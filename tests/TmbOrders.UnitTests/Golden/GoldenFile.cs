using System.Runtime.CompilerServices;

namespace TmbOrders.UnitTests.Golden;

/// <summary>
/// Golden tests (snapshot): compara a saída atual com um arquivo "golden" versionado no repositório.
///
///  - Se o contrato mudar sem querer, o teste falha e mostra a diferença.
///  - Se a mudança for intencional, regenere os arquivos com a variável UPDATE_GOLDEN=1,
///    revise o diff no Git e faça commit. Assim, toda mudança de contrato fica explícita no code review.
/// </summary>
internal static class GoldenFile
{
    private const string UpdateVariable = "UPDATE_GOLDEN";

    public static void AssertMatches(string name, string actual, [CallerFilePath] string callerFilePath = "")
    {
        var directory = Path.GetDirectoryName(callerFilePath)!;
        var path = Path.Combine(directory, $"{name}.golden.json");
        var normalizedActual = Normalize(actual);

        var update = Environment.GetEnvironmentVariable(UpdateVariable) == "1";

        if (update || !File.Exists(path))
        {
            File.WriteAllText(path, normalizedActual);

            if (!update)
            {
                Assert.Fail($"Arquivo golden criado em '{path}'. Revise o conteúdo, faça commit e rode os testes novamente.");
            }

            return;
        }

        var expected = Normalize(File.ReadAllText(path));

        Assert.True(
            expected == normalizedActual,
            $"""
            O contrato '{name}' mudou em relação ao arquivo golden ({path}).
            Se a mudança for intencional, rode os testes com {UpdateVariable}=1, revise o diff e faça commit.

            --- esperado (golden) ---
            {expected}
            --- obtido ---
            {normalizedActual}
            """);
    }

    // Ignora diferenças de quebra de linha (Windows x Linux) e espaços no fim do arquivo.
    private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd() + "\n";
}
