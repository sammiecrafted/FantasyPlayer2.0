using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace FantasyPlayer.Config
{
    public class AppleMusicSettings : INotifyPropertyChanged
    {
        private bool isDirty;
        public bool IsDirty
        {
            get => isDirty;
            set => isDirty = value;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        private string developerToken = string.Empty;
        private string teamId = string.Empty;
        private string keyId = string.Empty;
        private string musicUserToken = string.Empty;
        private string musicUserId = string.Empty;
        private bool limitedAccess;
        private bool isLoggedIn;

        public string DeveloperToken
        {
            get => developerToken;
            set => SetField(ref developerToken, value);
        }

        public string TeamId
        {
            get => teamId;
            set => SetField(ref teamId, value);
        }

        public string KeyId
        {
            get => keyId;
            set => SetField(ref keyId, value);
        }

        public string MusicUserToken
        {
            get => musicUserToken;
            set => SetField(ref musicUserToken, value);
        }

        public string MusicUserId
        {
            get => musicUserId;
            set => SetField(ref musicUserId, value);
        }

        public bool LimitedAccess
        {
            get => limitedAccess;
            set => SetField(ref limitedAccess, value);
        }

        public bool IsLoggedIn
        {
            get => isLoggedIn;
            set => SetField(ref isLoggedIn, value);
        }

        public void ClearLogin()
        {
            MusicUserToken = string.Empty;
            MusicUserId = string.Empty;
            IsLoggedIn = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            IsDirty = true;
            return true;
        }
    }
}
