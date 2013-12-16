using Microsoft.Live;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace SharEasy.Common {
    public class Upload : INotifyPropertyChanged {
        public CancellationTokenSource CancellationToken { get; set; }

        public StorageFile File { get; set; }

        public double Progress { get; set; }

        public Progress<LiveOperationProgress> ProgressHandler { get; set; }

        public string Name { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler == null) {
                return;
            }
            handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetProgressValue(double progress) {
            this.Progress = progress;
            OnPropertyChanged("Value");
        }

        public Upload(StorageFile file) {
            this.File = file;
            this.Name = file.Name;
            this.Progress = 0;
        }
    }
}
