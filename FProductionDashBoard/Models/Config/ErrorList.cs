using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FProductionDashBoard.Models
{
    public class ErrorList
    {
        public int ErrorId { get; set; } // 代理PK
        public string ErrorCode { get; set; } = string.Empty;   // UNIQUE
        public int? TypeId { get; set; } = 1;
        public int Severity { get; set; } = 0;
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
        public ListType? Type { get; set; } // 對應 ListType
        public ICollection<ErrorTranslation> Translations { get; set; } = new List<ErrorTranslation>(); // 對應多筆翻譯
    }

    public class ErrorTranslation
    {
        public int TranslationId { get; set; } // PK
        public string ErrorCode { get; set; } = string.Empty; // FK UNIQUE
        public string LanguageCode { get; set; } = string.Empty; // UNIQUE zh-TW, en-US, vi-VN
        public string? Message { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
        public ErrorList? Error { get; set; } // 對應 ErrorList
    }
    public class ErrorListConfiguration : IEntityTypeConfiguration<ErrorList>
    {
        public void Configure(EntityTypeBuilder<ErrorList> builder)
        {
            builder.ToTable("error_list");

            builder.HasKey(e => e.ErrorId);
            builder.Property(e => e.ErrorId).HasColumnName("error_id").ValueGeneratedOnAdd();

            builder.Property(e => e.ErrorCode).HasColumnName("error_code");
            builder.Property(e => e.TypeId).HasColumnName("type_id");
            builder.Property(e => e.Severity).HasColumnName("severity");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()"); ;
            builder.Property(e => e.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()"); ;

            builder.HasIndex(e => e.ErrorCode).IsUnique(); // 保證 error_code 唯一
            builder.HasOne(t => t.Type)
                   .WithMany(e => e.ErrorLists)
                   .HasForeignKey(t => t.TypeId);
        }
    }
    public class ErrorTranslationConfiguration : IEntityTypeConfiguration<ErrorTranslation>
    {
        public void Configure(EntityTypeBuilder<ErrorTranslation> builder)
        {
            builder.ToTable("error_translation");

            builder.HasKey(t => t.TranslationId);
            builder.Property(t => t.TranslationId).HasColumnName("translation_id").ValueGeneratedOnAdd();

            builder.Property(t => t.ErrorCode).HasColumnName("error_code");
            builder.Property(t => t.LanguageCode).HasColumnName("language_code");
            builder.Property(t => t.Message).HasColumnName("message");
            builder.Property(e => e.CreateAt).HasColumnName("create_at").HasDefaultValueSql("GETDATE()"); ;
            builder.Property(e => e.UpdateAt).HasColumnName("update_at").HasDefaultValueSql("GETDATE()"); ;

            builder.HasOne(t => t.Error)
                   .WithMany(e => e.Translations)
                   .HasForeignKey(t => t.ErrorCode)
                   .HasPrincipalKey(e => e.ErrorCode);
            builder.HasIndex(t => new { t.ErrorCode, t.LanguageCode }).IsUnique();
        }
    }
}
