// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests
{
    public class HttpProtocolSelectionTests : TestApplicationErrorLoggerLoggedTest
    {
        [Fact]
        public Task Server_NoProtocols_Error()
        {
            return TestError<InvalidOperationException>(HttpProtocols.None, CoreStrings.EndPointRequiresAtLeastOneProtocol);
        }

        [Fact]
        public Task Server_Http1AndHttp2_Cleartext_Http1Default()
        {
            return TestSuccess(HttpProtocols.Http1AndHttp2, "GET / HTTP/1.1\r\nHost:\r\n\r\n", "HTTP/1.1 200 OK");
        }

        [Fact]
        public Task Server_Http1Only_Cleartext_Success()
        {
            return TestSuccess(HttpProtocols.Http1, "GET / HTTP/1.1\r\nHost:\r\n\r\n", "HTTP/1.1 200 OK");
        }

        [Fact]
        public Task Server_Http2Only_Cleartext_Success()
        {
            // Expect a SETTINGS frame (type 0x4) with default settings
            return TestSuccess(HttpProtocols.Http2, Encoding.ASCII.GetString(Http2Connection.ClientPreface),
                "\x00\x00\x06\x04\x00\x00\x00\x00\x00\x00\x03\x00\x00\x00\x64");
        }

        private async Task TestSuccess(HttpProtocols serverProtocols, string request, string expectedResponse)
        {
            var testContext = new TestServiceContext(LoggerFactory);
            var listenOptions = new ListenOptions(new IPEndPoint(IPAddress.Loopback, 0))
            {
                Protocols = serverProtocols
            };

            using (var server = new TestServer(context => Task.CompletedTask, testContext, listenOptions))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.Send(request);
                    await connection.Receive(expectedResponse);
                }
            }
        }

        private async Task TestError<TException>(HttpProtocols serverProtocols, string expectedErrorMessage)
            where TException : Exception
        {
            var testContext = new TestServiceContext(LoggerFactory);
            var listenOptions = new ListenOptions(new IPEndPoint(IPAddress.Loopback, 0))
            {
                Protocols = serverProtocols
            };

            using (var server = new TestServer(context => Task.CompletedTask, testContext, listenOptions))
            {
                using (var connection = server.CreateConnection())
                {
                    await connection.WaitForConnectionClose();
                }
            }

            Assert.Single(TestApplicationErrorLogger.Messages, message => message.LogLevel == LogLevel.Error
                && message.EventId.Id == 0
                && message.Message == expectedErrorMessage);
        }
    }
}
