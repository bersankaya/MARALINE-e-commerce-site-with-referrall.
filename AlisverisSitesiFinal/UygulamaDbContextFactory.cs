using AlisverisSitesiFinal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace AlisverisSitesiFinal.Data
{
    public class UygulamaDbContextFactory : IDesignTimeDbContextFactory<UygulamaDbContext>
    {
        public UygulamaDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var builder = new DbContextOptionsBuilder<UygulamaDbContext>();
            builder.UseSqlServer(connectionString);

            return new UygulamaDbContext(builder.Options);
        }
    }
}