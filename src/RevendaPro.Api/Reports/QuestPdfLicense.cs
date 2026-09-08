using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// A licença Community do QuestPDF, declarada assim que este assembly carrega.
    ///
    /// O <c>Program.cs</c> a declara de novo, por legibilidade; aqui ela vale também para o
    /// teste e para qualquer ferramenta que gere um documento sem subir a API — o QuestPDF
    /// confere a licença antes de desenhar a primeira página, e um construtor estático só
    /// rodaria quando alguém tocasse na classe. Ver ADR-0007.
    /// </summary>
    internal static class QuestPdfLicense
    {
        [ModuleInitializer]
        internal static void Declare() => QuestPDF.Settings.License = LicenseType.Community;
    }
}
