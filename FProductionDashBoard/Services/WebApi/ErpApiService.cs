using System.Net.Http;
using System.Net.Http.Json;
using FProductionDashBoard.Dtos;
using Microsoft.Extensions.Options;

namespace FProductionDashBoard.Services.WebApi;

public class ErpApiService : IErpApiService
{
    private readonly HttpClient _http;
    private readonly string _factoryArea;

    public ErpApiService(HttpClient http, IOptions<ErpApiOptions> options)
    {
        _http = http;
        _factoryArea = options.Value.FactoryArea;
    }

    public async Task<EmpInfoDto?> GetEmpInfoByCardAsync(string empCardNumber)
    {
        try
        {
            return await _http.GetFromJsonAsync<EmpInfoDto>(
                $"api/Po/GetEmpInfoByCard/{Uri.EscapeDataString(_factoryArea)}/{Uri.EscapeDataString(empCardNumber)}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<PartInfoDto?> GetPartInfoByNoAsync(string partNo)
    {
        try
        {
            // 查無件號時 ERP 回傳 body "null"，GetFromJsonAsync 直接得 C# null
            return await _http.GetFromJsonAsync<PartInfoDto>(
                $"api/So/GetSompnodfn3V?bmstype={Uri.EscapeDataString(partNo)}");
        }
        catch
        {
            return null;
        }
    }
}
