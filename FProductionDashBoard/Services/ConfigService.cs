namespace FProductionDashBoard.Services
{
    public class ConfigService<T> : IConfigService<T> where T : new()
    {
        private readonly string _fileName;

        public ConfigService(string fileName) { _fileName = fileName; }

        public T Current { get; private set; } = new();

        public void Load()       => Current = JsonDataService.Load<T>(_fileName);
        public void Save(T dto)  { Current = dto; JsonDataService.Save(dto, _fileName); }
    }
}
