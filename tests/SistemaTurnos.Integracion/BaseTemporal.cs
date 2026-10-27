using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

// Exclusivamente para pruebas: nunca instala ni elimina SistemaTurnos del usuario.
internal sealed class BaseTemporal : IAsyncDisposable
{
    private readonly string nombre="SistemaTurnos_Prueba_"+Guid.NewGuid().ToString("N");
    private readonly string master;
    public string Cadena {get;}
    private BaseTemporal(string servidor)
    {
        var b=new SqlConnectionStringBuilder { DataSource=servidor,InitialCatalog="master",IntegratedSecurity=true,ConnectTimeout=15 };
        // LocalDB usa transporte local; nunca desactivar TLS para un servidor remoto.
        if(servidor.StartsWith("(localdb)\\",StringComparison.OrdinalIgnoreCase))b.Encrypt=SqlConnectionEncryptOption.Optional;
        master=b.ConnectionString;b.InitialCatalog=nombre;Cadena=b.ConnectionString;
    }
    public static async Task<BaseTemporal> CrearAsync(string scripts,string servidor)
    {
        var b=new BaseTemporal(servidor);
        try
        {
            using var cn=new SqlConnection(b.master);await cn.OpenAsync();
            foreach(string archivo in new[]{"Instalar_LocalDB.sql","007_RolServidorMinimo.sql"})
            {
                string sql=(await File.ReadAllTextAsync(Path.Combine(scripts,archivo))).Replace("SistemaTurnos",b.nombre,StringComparison.Ordinal);
                // El rol conserva el nombre esperado por las pruebas.
                sql=sql.Replace(b.nombre+"_ApiRuntime","SistemaTurnos_ApiRuntime",StringComparison.Ordinal);
                foreach(string lote in Regex.Split(sql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
                    if(!string.IsNullOrWhiteSpace(lote))
                    {using var cmd=new SqlCommand(lote,cn){CommandTimeout=120};await cmd.ExecuteNonQueryAsync();}
            }
            return b;
        }
        catch {await b.DisposeAsync();throw;}
    }
    public async ValueTask DisposeAsync()
    {
        if(!Regex.IsMatch(nombre,@"\ASistemaTurnos_Prueba_[a-f0-9]{32}\z"))throw new InvalidOperationException("Nombre temporal inválido.");
        SqlConnection.ClearAllPools();
        using var cn=new SqlConnection(master);await cn.OpenAsync();
        using var cmd=new SqlCommand($"IF DB_ID(N'{nombre}') IS NOT NULL BEGIN ALTER DATABASE [{nombre}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{nombre}]; END;",cn){CommandTimeout=60};
        await cmd.ExecuteNonQueryAsync();
    }
}
