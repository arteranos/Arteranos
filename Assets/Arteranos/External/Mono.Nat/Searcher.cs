//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Mono.Nat.Logging;

namespace Mono.Nat
{
    abstract class Searcher : ISearcher
    {
        static Logger Log { get; } = Logger.Create (nameof (Searcher));

        protected static readonly TimeSpan SearchPeriod = TimeSpan.FromMinutes (5);

        public event EventHandler<DeviceEventArgs> DeviceFound;
        public event EventHandler<DeviceEventUnknownArgs> UnknownDeviceFound;

        public bool Listening { get; private set; }
        public abstract NatProtocol Protocol { get; }

        Dictionary<NatDevice, NatDevice> Devices { get; }
        protected SocketGroup Clients { get; }

        protected CancellationTokenSource Cancellation { get; }
        SemaphoreSlim Locker = new SemaphoreSlim (1, 1);

        protected Searcher (SocketGroup clients)
        {
            Clients = clients;
            Cancellation = new CancellationTokenSource ();
            Devices = new Dictionary<NatDevice, NatDevice> ();
        }

        protected void BeginListening ()
        {
            // Begin listening, if we are not already listening.
            if (!Listening) {
                Listening = true;
                ListenAsync (Cancellation.Token);
            }
        }

        public void Dispose ()
        {
            Clients.Dispose ();
            Cancellation.Cancel ();
        }

        async void ListenAsync (CancellationToken token)
        {
            try {
                var listens = new List<Task> ();
                foreach (var udpClient in Clients.Clients)
                    listens.Add (ListenOneAsync (udpClient, token));
                await Task.WhenAll (listens);
                token.ThrowIfCancellationRequested ();
            } catch (OperationCanceledException) {
                Listening = false;
                return;
            } catch(Exception ex) {
                Log.ExceptionFormated (ex, "Unhandled exception listening for clients in {0}", GetType().Name);
            }
        }

        async Task ListenOneAsync (System.Net.Sockets.UdpClient udpClient, CancellationToken token)
        {
            while (!token.IsCancellationRequested) {
                try {
#if NETSTANDARD2_0 || NETSTANDARD2_1
                    var data = await udpClient.ReceiveAsync ();
#else
                    var data = await udpClient.ReceiveAsync (token);
#endif
                    var localEndPoint = (IPEndPoint) udpClient.Client.LocalEndPoint;
                    token.ThrowIfCancellationRequested ();

                    using (await Locker.EnterAsync (token))
                        await HandleMessageReceived (localEndPoint.Address, data.Buffer, data.RemoteEndPoint, false, token).ConfigureAwait (false);
                } catch (OperationCanceledException) {
                    return;
                } catch (Exception) {
                    // Ignore any errors
                }
            }
        }

        public Task HandleMessageReceived (IPAddress localAddress, byte[] response, IPEndPoint endpoint, CancellationToken token)
            => HandleMessageReceived (localAddress, response, endpoint, true, token);

        protected abstract Task HandleMessageReceived (IPAddress localAddress, byte[] response, IPEndPoint endpoint, bool externalEvent, CancellationToken token);

        public void BeginSearching ()
        {
            // Create a CancellationTokenSource for the search we're about to perform.
            BeginListening ();
            SearchAsync (null, SearchPeriod, Cancellation.Token);
        }

        public void BeginSearching (IPAddress gatewayAddress)
        {
            BeginListening ();
            SearchAsync (gatewayAddress, null, Cancellation.Token);
        }

        protected abstract void SearchAsync (IPAddress gatewayAddress, TimeSpan? repeatInterval, CancellationToken token);

        protected void RaiseDeviceUnknown (IPAddress address, EndPoint remote, string response, NatProtocol protocol)
        {
            UnknownDeviceFound?.Invoke (this, new DeviceEventUnknownArgs (address, remote, response, protocol));
        }

        protected void RaiseDeviceFound (NatDevice device)
        {
            NatDevice actualDevice;
            lock (Devices) {
                if (Devices.TryGetValue (device, out actualDevice))
                    actualDevice.LastSeen = DateTime.UtcNow;
                else
                    Devices[device] = device;
            }
            // If we did not find the device in the dictionary, raise an event as it's the first time
            // we've encountered it!
            if (actualDevice == null)
                DeviceFound?.Invoke (this, new DeviceEventArgs (device));
        }
    }
}
