using System;
using System.Windows.Threading;

namespace WpfApp1.Services
{
    public class CinePlayerService
    {
        private readonly DispatcherTimer _timer;
        private int _currentSlice;
        private int _maxSlice;
        private Action<int> _onSliceChanged;

        public bool IsPlaying => _timer.IsEnabled;

        public CinePlayerService()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += Timer_Tick;
        }

        public void SetCallback(Action<int> onSliceChanged)
        {
            _onSliceChanged = onSliceChanged;
        }

        public void Play(int currentSlice, int maxSlice)
        {
            _currentSlice = currentSlice;
            _maxSlice = maxSlice;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_maxSlice == 0) return;
            _currentSlice = (_currentSlice + 1) % (_maxSlice + 1);
            _onSliceChanged?.Invoke(_currentSlice);
        }
    }
}