using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Serialization;
using FindRepl.Commands;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell.Interop;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Threading;

namespace FindRepl.ViewModels
{
    class MainViewModel : INotifyPropertyChanged
    {
        private BackgroundWorker _bwFinder;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<FileData> FoundFilesCollection { get; set; }

        public MainViewModel()
        {
            
            currentDir = string.Empty;
            Mask = string.Empty;
            ExcludeMask = string.Empty;
            ExcludeDir = ExcludeMask = string.Empty;
            FoundFilesCollection = new ObservableCollection<FileData>();
            _bwFinder = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _bwFinder.DoWork += BwDoWork;
            _bwFinder.ProgressChanged += BwProgressChanges;
            _bwFinder.RunWorkerCompleted += BwFindFilesRunWorkerCompleted;
        }

        private void BwProgressChanges(object sender, ProgressChangedEventArgs e)
        {
            Progress = e.ProgressPercentage;
            if (e.UserState as FileData != null) FoundFilesCollection.Add(e.UserState as FileData);
        }

        private void BwDoWork(object sender, DoWorkEventArgs e)
        {
            
            var bw = (BackgroundWorker)sender;
            string[] filesPathes = Directory.GetFiles(currentDir, string.IsNullOrWhiteSpace(Mask) ? "*.*" : Mask, SearchOption.AllDirectories);
            if (filesPathes.Length == 0)
            {
                e.Cancel = true;
            }
            else
            {
                var PathCollection = Philtered(filesPathes);
                CounterFiles = PathCollection.Count();
                for (int i = 0; i < PathCollection.Count(); i++)
                {
                    if (_bwFinder.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }
                    ReplacerFile(PathCollection.ElementAt(i), (int)((i + 1) / (float)PathCollection.Count() * 100), bw);
                    bw.ReportProgress((int)((i + 1) / (float)PathCollection.Count() * 100));
                    Thread.Sleep(1000);
                }
            }

        }

        private void ReplacerFile(string path, int percent, BackgroundWorker bw)
        {
            if (IsCorrected == false)
            {
                using (var input = File.OpenText(path))
                {
                    string line;
                    while ((line = input.ReadLine()) != null)
                    {
                        if (line.Contains(dataFind))
                        {
                            bw.ReportProgress(percent, new FileData()
                            {
                                FilePath = path,
                            });
                        }
                    }
                }
            }
            else
            {
                bool SubString = false;
                using (var input = File.OpenText(path))
                {
                    string line;
                    while ((line = input.ReadLine()) != null)
                    {
                        if (line.Contains(dataFind))
                        {
                            SubString = true;
                            bw.ReportProgress(percent, new FileData()
                            {
                                FilePath = path,
                            });
                        }
                    }
                }
                if (SubString == true)
                {
                    using (var input = File.OpenText(path))
                    using (var output = new StreamWriter(path + ".tmp"))
                    {
                        string line;
                        while ((line = input.ReadLine()) != null)
                        {
                            line = line.Contains(dataFind) ? line.Replace(dataFind, dataReplace) : line;
                            output.WriteLine(line);
                        }
                    }
                    File.Replace(path + ".tmp", path, null);
                }
            }
        }

        private IEnumerable<string> Philtered(string[] filesPathes)
        {
            return filesPathes.Where(x => IsExcluded(x) == false);
        }


        private bool IsExcluded(string path)
        {
            if (ExcludeMask != string.Empty)
                return new Regex(ExcludeMask.Replace(".", "[.]").Replace("*", ".*").Replace("?", ".")).IsMatch(path)
                        || path.Contains($"/{ExcludeDir}/");
            return path.Contains($"/{ExcludeDir}/");
        }

        private void BwFindFilesRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsBusy = false;
           
            CommandManager.InvalidateRequerySuggested();
        }

        public void OpenDir()
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog();
            dlg.InitialDirectory = "C:\\";
            dlg.IsFolderPicker = true;
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                currentDir = dlg.FileName;
            }
        }

        public ICommand PickDir
        {
            get => new RelayCommand(OpenDir, () => true);
        }

        public ICommand CancelButton
        {
            get => new RelayCommand(() =>
            {
                if (IsBusy)
                {
                    _bwFinder.CancelAsync();
                }
            }, () => IsBusy);
        }
        public class FileData
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
        }

        private string GetName (string path)
        {
            return System.IO.Path.GetFileName(path);
        }

        private string GetPath(string path)
        {
            return ".\\" + System.IO.Path.GetDirectoryName(path).Remove(0, currentDir.Length); 
        }

        private string _currentDir;
        public string currentDir
        {
            get => _currentDir;
            set
            {
                _currentDir = value;
                OnPropertyChanged(nameof(currentDir));
            }
        }
        private string _exDir;
        public string ExcludeDir
        {
            get => _exDir;
            set
            {
                _exDir = value;
                OnPropertyChanged(nameof(ExcludeDir));
            }
        }

        private bool _includeSubDirectories;
        public bool IncludeSubDirectories
        {
            get => _includeSubDirectories;
            set
            {
                _includeSubDirectories = value;
                OnPropertyChanged(nameof(IncludeSubDirectories));
            }
        }

        private bool _iscorrected;
        public bool IsCorrected
        {
            get => _iscorrected;
            set
            {
                _iscorrected = value;
                OnPropertyChanged(nameof(IsCorrected));
            }
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress == value) return;
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        private int _totalCountFiles;
        public int CounterFiles
        {
            get => _totalCountFiles;
            set
            {
                _totalCountFiles = value;
                OnPropertyChanged(nameof(CounterFiles));
            }
        }

        private string _mask;
        public string Mask
        {
            get => _mask;
            set
            {
                _mask = value;
                OnPropertyChanged(nameof(Mask));
            }
        }

        private string _excludeMask;
        public string ExcludeMask
        {
            get => _excludeMask;
            set
            {
                _excludeMask = value;
                OnPropertyChanged(nameof(ExcludeMask));
            }
        }

        public ICommand FinderText
        {
            get => new RelayCommand(() =>
            {
                IsBusy = true;
                IsCorrected = false;
                FoundFilesCollection.Clear();
                _bwFinder.RunWorkerAsync(this);
            }, () => IsBusy == false
                     && string.IsNullOrWhiteSpace(currentDir) == false
                     && string.IsNullOrWhiteSpace(dataFind) == false);
        }

        public ICommand ReplacerText
        {
            get => new RelayCommand(() =>
            {
                IsBusy = true;
                IsCorrected = true;
                FoundFilesCollection.Clear();
                _bwFinder.RunWorkerAsync(this);
            }, () => IsBusy == false
                     && string.IsNullOrWhiteSpace(currentDir) == false
                     && string.IsNullOrWhiteSpace(dataFind) == false);
        }

        private string _dataFind;
        public string dataFind
        {
            get => _dataFind;
            set
            {
                _dataFind = value;
                OnPropertyChanged(nameof(dataFind));
            }
        }

        private string _dataReplace;
        public string dataReplace
        {
            get => _dataReplace;
            set
            {
                _dataReplace = value;
                OnPropertyChanged(nameof(dataReplace));
            }
        }
    }

}
