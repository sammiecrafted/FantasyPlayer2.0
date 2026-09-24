namespace FantasyPlayer.Config
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    public class LocalSettings : INotifyPropertyChanged
    {
        private bool isDirty;
        public bool IsDirty
        {
            get => isDirty;
            private set => SetField(ref isDirty, value, false);
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        private string host = "127.0.0.1";
        public string Host
        {
            get => host;
            set => SetField(ref host, value);
        }

        private int port = 6600;
        public int Port
        {
            get => port;
            set => SetField(ref port, value);
        }

        private string password = string.Empty;
        public string Password
        {
            get => password;
            set => SetField(ref password, value);
        }

        private string musicFolder = string.Empty;
        public string MusicFolder
        {
            get => musicFolder;
            set => SetField(ref musicFolder, value);
        }

        public List<RadioStation> Stations { get; set; } = new List<RadioStation>();

        public void AddStation(RadioStation station)
        {
            Stations.Add(station);
            IsDirty = true;
        }

        public void RemoveStationAt(int index)
        {
            if (index >= 0 && index < Stations.Count)
            {
                Stations.RemoveAt(index);
                IsDirty = true;
            }
        }

        public void EditStation(int index, string name, string url)
        {
            if (index < 0 || index >= Stations.Count)
            {
                return;
            }

            Stations[index].Name = name;
            Stations[index].Url = url;
            IsDirty = true;
        }

        public LocalSettings()
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, bool markDirty = true, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            if (markDirty)
            {
                IsDirty = true;
            }

            return true;
        }
    }

    public class RadioStation
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}