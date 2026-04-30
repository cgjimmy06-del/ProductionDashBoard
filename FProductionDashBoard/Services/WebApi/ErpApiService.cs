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
}
