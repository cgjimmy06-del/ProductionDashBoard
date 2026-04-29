using System.Text.Json.Serialization;

namespace FProductionDashBoard.Dtos;

public class EmpInfoDto
{
    [JsonPropertyName("emp_no")]
    public string EmpNo { get; set; } = "";

    [JsonPropertyName("id_nm")]
    public string Name { get; set; } = "";

    [JsonPropertyName("dept_no")]
    public string DeptNo { get; set; } = "";

    [JsonPropertyName("dept_nm")]
    public string DeptName { get; set; } = "";
}
