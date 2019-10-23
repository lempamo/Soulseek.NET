// <copyright file="SoulseekClientTests.cs" company="JP Dillingham">
//     Copyright (c) JP Dillingham. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty
//     of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace Soulseek.Tests.Integration
{
    using System.Threading.Tasks;
    using Soulseek.Options;
    using Xunit;

    public class SoulseekClientTests
    {
        [Trait("Category", "Connectivity")]
        [Fact(DisplayName = "Client connects")]
        public async Task Client_Connects()
        {
            using (var client = new SoulseekClient())
            {
                var ex = await Record.ExceptionAsync(() => client.ConnectAsync());

                Assert.Null(ex);
                Assert.Equal(SoulseekClientStates.Connected, client.State);
            }
        }

        [Trait("Category", "Connectivity")]
        [Fact(DisplayName = "Client connect raises StateChanged event")]
        public async Task Client_Connect_Raises_StateChanged_Event()
        {
            SoulseekClientStateChangedEventArgs args = null;

            using (var client = new SoulseekClient())
            {
                client.StateChanged += (sender, e) => args = e;

                var ex = await Record.ExceptionAsync(() => client.ConnectAsync());

                Assert.Null(ex);
                Assert.Equal(SoulseekClientStates.Connected, client.State);
                Assert.Equal(SoulseekClientStates.Connected, args.State);
            }
        }

        [Trait("Category", "Connectivity")]
        [Fact(DisplayName = "Client disconnects")]
        public async Task Client_Disconnects()
        {
            using (var client = new SoulseekClient())
            {
                await client.ConnectAsync();

                var ex = Record.Exception(() => client.Disconnect());

                Assert.Null(ex);
                Assert.Equal(SoulseekClientStates.Disconnected, client.State);
            }
        }

        [Trait("Category", "Connectivity")]
        [Fact(DisplayName = "Client disconnect raises StateChanged event")]
        public async Task Client_Disconnect_Raises_StateChanged_Event()
        {
            SoulseekClientStateChangedEventArgs args = null;

            using (var client = new SoulseekClient())
            {
                await client.ConnectAsync();

                client.StateChanged += (sender, e) => args = e;

                var ex = Record.Exception(() => client.Disconnect());

                Assert.Null(ex);
                Assert.Equal(SoulseekClientStates.Disconnected, client.State);
                Assert.Equal(SoulseekClientStates.Disconnected, args.State);
            }
        }

        [Trait("Category", "GetNextToken")]
        [Fact(DisplayName = "GetNextToken returns sequential tokens")]
        public void GetNextToken_Returns_Sequential_Tokens()
        {
            using (var s = new SoulseekClient())
            {
                var t1 = s.GetNextToken();
                var t2 = s.GetNextToken();

                Assert.Equal(t1 + 1, t2);
            }
        }

        [Trait("Category", "GetNextToken")]
        [Fact(DisplayName = "GetNextToken rolls over at int.MaxValue")]
        public void GetNextToken_Rolls_Over_At_Int_MaxValue()
        {
            using (var s = new SoulseekClient(
                new ClientOptions(startingToken: int.MaxValue)))
            {
                var t1 = s.GetNextToken();
                var t2 = s.GetNextToken();

                Assert.Equal(int.MaxValue, t1);
                Assert.Equal(0, t2);
            }
        }
    }
}
