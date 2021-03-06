﻿// <copyright file="MessageConnection.cs" company="JP Dillingham">
//     Copyright (c) JP Dillingham. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License
//     as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty
//     of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace Soulseek.Network
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Soulseek.Exceptions;
    using Soulseek.Network.Tcp;

    /// <summary>
    ///     Provides client connections to the Soulseek network.
    /// </summary>
    internal sealed class MessageConnection : Connection, IMessageConnection
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MessageConnection"/> class.
        /// </summary>
        /// <param name="username">The username of the peer associated with the connection, if applicable.</param>
        /// <param name="ipEndPoint">The remote IP endpoint of the connection.</param>
        /// <param name="options">The optional options for the connection.</param>
        /// <param name="tcpClient">The optional TcpClient instance to use.</param>
        internal MessageConnection(string username, IPEndPoint ipEndPoint, ConnectionOptions options = null, ITcpClient tcpClient = null)
            : this(ipEndPoint, options, tcpClient)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException($"The username must not be a null or empty string, or one consisting only of whitespace", nameof(username));
            }

            Username = username;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="MessageConnection"/> class.
        /// </summary>
        /// <param name="ipEndPoint">The remote IP endpoint of the connection.</param>
        /// <param name="options">The optional options for the connection.</param>
        /// <param name="tcpClient">The optional TcpClient instance to use.</param>
        internal MessageConnection(IPEndPoint ipEndPoint, ConnectionOptions options = null, ITcpClient tcpClient = null)
            : base(ipEndPoint, options, tcpClient)
        {
            // bind the connected event to begin reading upon connection. if we received a connected client, this will never fire
            // and the read loop must be started via ReadContinuouslyAsync().
            Connected += (sender, e) =>
            {
                // if Username is empty, this is a server connection. begin reading continuously, and throw on exception.
                if (string.IsNullOrEmpty(Username))
                {
                    Task.Run(() => ReadContinuouslyAsync()).ForgetButThrowWhenFaulted<ConnectionException>();
                }
                else
                {
                    // swallow exceptions from peer connections; these will be handled by timeouts.
                    Task.Run(() => ReadContinuouslyAsync()).Forget();
                }
            };
        }

        /// <summary>
        ///     Occurs when message data is received.
        /// </summary>
        public event EventHandler<MessageDataReadEventArgs> MessageDataRead;

        /// <summary>
        ///     Occurs when a new message is read in its entirety.
        /// </summary>
        public event EventHandler<MessageReadEventArgs> MessageRead;

        /// <summary>
        ///     Occurs when a new message is received, but before it is read.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        ///     Gets the unique identifier for the connection.
        /// </summary>
        public override ConnectionKey Key => new ConnectionKey(Username, IPEndPoint);

        /// <summary>
        ///     Gets a value indicating whether the internal continuous read loop is running.
        /// </summary>
        public bool ReadingContinuously { get; private set; }

        /// <summary>
        ///     Gets the username of the peer associated with the connection, if applicable.
        /// </summary>
        public string Username { get; private set; } = string.Empty;

        /// <summary>
        ///     Begins the internal continuous read loop, if it has not yet started.
        /// </summary>
        public void StartReadingContinuously()
        {
            if (!ReadingContinuously)
            {
                Task.Run(() => ReadContinuouslyAsync()).Forget();
            }
        }

        private async Task ReadContinuouslyAsync()
        {
            if (ReadingContinuously)
            {
                return;
            }

            ReadingContinuously = true;
            byte[] codeBytes = null;

            void RaiseMessageDataRead(object sender, ConnectionDataEventArgs e)
            {
                Interlocked.CompareExchange(ref MessageDataRead, null, null)?
                    .Invoke(this, new MessageDataReadEventArgs(codeBytes, e.CurrentLength, e.TotalLength));
            }

            try
            {
                while (true)
                {
                    try
                    {
                        var message = new List<byte>();

                        var lengthBytes = await ReadAsync(4, CancellationToken.None).ConfigureAwait(false);
                        var length = BitConverter.ToInt32(lengthBytes, 0);
                        message.AddRange(lengthBytes);

                        codeBytes = await ReadAsync(4, CancellationToken.None).ConfigureAwait(false);
                        message.AddRange(codeBytes);

                        RaiseMessageDataRead(this, new ConnectionDataEventArgs(0, length - 4));

                        Interlocked.CompareExchange(ref MessageReceived, null, null)?
                            .Invoke(this, new MessageReceivedEventArgs(length, codeBytes));

                        DataRead += RaiseMessageDataRead;

                        var payloadBytes = await ReadAsync(length - 4, CancellationToken.None).ConfigureAwait(false);
                        message.AddRange(payloadBytes);

                        var messageBytes = message.ToArray();
                        Interlocked.CompareExchange(ref MessageRead, null, null)?
                            .Invoke(this, new MessageReadEventArgs(messageBytes));
                    }
                    finally
                    {
                        DataRead -= RaiseMessageDataRead;
                    }
                }
            }
            finally
            {
                ReadingContinuously = false;
            }
        }
    }
}