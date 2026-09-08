namespace RevendaPro.Api.Reports
{
    /// <summary>
    /// Uma coluna de planilha ou de tabela: o título e como ler o valor de cada linha.
    ///
    /// O valor sai <b>tipado</b> — <c>decimal</c>, <c>DateOnly</c>, <c>int</c> —, e cada formato
    /// decide o que fazer com ele: o Excel guarda número e data de verdade, que é o que permite
    /// somar e ordenar; o PDF e o CSV formatam em <c>pt-BR</c>. Uma coluna que já entregasse
    /// texto pronto faria o Excel guardar "1.234,56" como texto, e a soma daria zero.
    /// </summary>
    /// <typeparam name="T">A linha.</typeparam>
    /// <param name="Header">O título da coluna, em português.</param>
    /// <param name="Value">Como ler o valor da linha.</param>
    /// <param name="AlignRight">Encostado à direita, como todo número.</param>
    public sealed record ReportColumn<T>(string Header, Func<T, object?> Value, bool AlignRight = false);

    /// <summary>Os nomes dos arquivos gerados: <c>NomeDDMMAAAA.ext</c>, o padrão da referência.</summary>
    public static class ReportFileName
    {
        /// <summary>Monta o nome com a data de hoje.</summary>
        /// <param name="name">O nome do documento, sem espaço nem acento: "Veiculos".</param>
        /// <param name="extension">A extensão, sem ponto.</param>
        /// <returns>Por exemplo, <c>Veiculos08092026.xlsx</c>.</returns>
        public static string For(string name, string extension) =>
            $"{name}{DateTime.Now.ToString("ddMMyyyy", DocumentTheme.Culture)}.{extension}";
    }
}
