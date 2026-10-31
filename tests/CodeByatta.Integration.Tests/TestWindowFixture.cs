using System;
using System.Threading;
using System.Windows.Forms;

namespace CodeByatta.Integration.Tests
{
    public class TestWindowFixture : IDisposable
    {
        public IntPtr Hwnd { get; private set; }
        private Form? _form;
        private Thread? _thread;
        private readonly ManualResetEvent _readyEvent = new(false);

        public TestWindowFixture(string title = "CodeByatta Test Window")
        {
            _thread = new Thread(() =>
            {
                _form = new Form
                {
                    Text = title,
                    Width = 400,
                    Height = 300,
                    StartPosition = FormStartPosition.Manual,
                    Left = 200,
                    Top = 200
                };

                _form.Shown += (s, e) =>
                {
                    Hwnd = _form.Handle;
                    _readyEvent.Set();
                };

                Application.Run(_form);
            });

            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();

            _readyEvent.WaitOne(3000);
        }

        public void Dispose()
        {
            if (_form != null && _form.IsHandleCreated)
            {
                try
                {
                    _form.Invoke(() => _form.Close());
                }
                catch { }
            }
            _readyEvent.Dispose();
        }
    }
}
