using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FProductionDashBoard.Models.Extra
{
    public class MesCustomerCode
    {
        public string BigCategories { get; set; } = string.Empty;
        public string MediumCategories { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
    }

    public class MesCustomerCodeConfiguration : IEntityTypeConfiguration<MesCustomerCode>
    {
        public void Configure(EntityTypeBuilder<MesCustomerCode> builder)
        {
            builder.HasNoKey();
            builder.ToTable("MES_CustomerCode");
            builder.Property(e => e.BigCategories).HasColumnName("Big_categories");
            builder.Property(e => e.MediumCategories).HasColumnName("Medium_categories");
            builder.Property(e => e.Customer).HasColumnName("Customer");
        }
    }
}
