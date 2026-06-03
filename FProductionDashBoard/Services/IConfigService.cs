namespace FProductionDashBoard.Services
{
    public interface IConfigService<T> where T : new()
    {
        T Current { get; }
        void Load();
        void Save(T dto);
    }
}
