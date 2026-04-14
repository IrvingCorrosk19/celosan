// Uso: dotnet run -- "tuContraseña"  → imprime solo el hash BCrypt (misma lib que la app).
if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: dotnet run -- <contraseña_en_claro>");
    return 1;
}

Console.Write(BCrypt.Net.BCrypt.HashPassword(args[0]));
return 0;
