using FProductionDashBoard.Services.WebApi;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FProductionDashBoard.Tests.Services
{
    public class ErpApiServiceTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string? _body;
            private readonly Exception? _throw;

            public FakeHttpMessageHandler(HttpStatusCode status, string? body)
            {
                _status = status;
                _body = body;
            }

            public FakeHttpMessageHandler(Exception toThrow) => _throw = toThrow;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_throw != null) throw _throw;
                return Task.FromResult(new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_body ?? "", Encoding.UTF8, "application/json")
                });
            }
        }

        private static ErpApiService CreateService(HttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake.local/erp/") };
            var options = Options.Create(new ErpApiOptions { FactoryArea = "FUS" });
            return new ErpApiService(httpClient, options);
        }

        [Fact]
        public async Task GetPartInfoByNoAsync_ValidResponse_ParsesProductNameAndBrand()
        {
            const string body = @"{""bmstype"":""74165027"",""p_stype_en"":""Zuma Max Fast DR Ti MRH"",""custg_abmm"":""CLW""}";
            var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, body));

            var result = await service.GetPartInfoByNoAsync("74165027");

            Assert.NotNull(result);
            Assert.Equal("74165027", result!.BmsType);
            Assert.Equal("Zuma Max Fast DR Ti MRH", result.ProductName);
            Assert.Equal("CLW", result.Brand);
        }

        [Fact]
        public async Task GetPartInfoByNoAsync_NullBody_ReturnsNull()
        {
            // 查無件號：ERP 回傳 body "null"
            var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

            var result = await service.GetPartInfoByNoAsync("00000000");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPartInfoByNoAsync_HttpError_ReturnsNull()
        {
            var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, ""));

            var result = await service.GetPartInfoByNoAsync("74165027");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetPartInfoByNoAsync_Exception_ReturnsNull()
        {
            var service = CreateService(new FakeHttpMessageHandler(new HttpRequestException("network down")));

            var result = await service.GetPartInfoByNoAsync("74165027");

            Assert.Null(result);
        }
    }
}
