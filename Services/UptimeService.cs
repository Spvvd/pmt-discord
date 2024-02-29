using System.Net;
using System.Net.NetworkInformation;

namespace pmt_discord
{
    public class UptimeService : EventArgs
    {
        public string ip;

        private bool _up = false;
        private int _delay = 5;
        private int _timeout = 10;

        private int _counter = 0;
        private static int _threshold = 5;

        public event EventHandler<ClientUpDownEventArgs> ClientDown;
        public event EventHandler<ClientUpDownEventArgs> ClientUp;

        public UptimeService(string ip)
        {
            this.ip = ip;
        }

        public async Task Run()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(_delay));

                // Skip ping when address not valid
                if (!Program.IsValidIpAddress(this.ip)) continue;

                Ping p = new Ping();

                var reply = await p.SendPingAsync(this.ip, _timeout);

                ClientUpDownEventArgs e = new ClientUpDownEventArgs();
                e.Ip = reply.Address;

                if (reply.Status == IPStatus.Success)
                {
                    _counter = 0;  // Successful pings always reset the counter
                    if (!_up)
                    {
                        _up = true;
                        e.Ping = reply.RoundtripTime;
                        ClientUp?.Invoke(this, e);
                    }
                }
                else
                {
                    if (_up)
                    {
                        // Increment counter until threshold is reached
                        _counter++;  
                        if (_counter >= _threshold)
                        {
                            _up = false;
                            ClientDown?.Invoke(this, e);
                        }
                    }
                }
            }
        }
    }

    public class ClientUpDownEventArgs : EventArgs
    {
        public IPAddress Ip { get; set; }
        public long Ping { get; set; }
    }
}
