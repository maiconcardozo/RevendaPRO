namespace RevendaPro.Shared.Helpers
{
    /// <summary>
    /// Que dia é hoje, para quem está na loja (M22).
    ///
    /// O contêiner roda em UTC, e às vinte e uma horas de Porto Alegre o UTC já virou o dia. Um
    /// gasto que vence hoje apareceria como vencido, e o caixa da noite mostraria a conta de
    /// amanhã em vermelho. Toda pergunta sobre <b>hoje</b> — o que vence, o que atrasou, a data
    /// que uma baixa recebe quando ninguém informa outra — passa por aqui.
    ///
    /// O fuso é fixo em -3, e jamais lido do sistema operacional: a imagem Docker é `alpine`
    /// sem base de fusos, e <c>TimeZoneInfo.FindSystemTimeZoneById</c> lançaria. O Brasil está
    /// sem horário de verão desde 2019; se ele voltar, é uma linha aqui, e um lugar só.
    /// </summary>
    public static class BrazilTime
    {
        private static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

        /// <summary>O momento de agora, no horário de Brasília.</summary>
        public static DateTime Now => DateTime.UtcNow + Offset;

        /// <summary>O dia de hoje, no horário de Brasília.</summary>
        public static DateOnly Today => DateOnly.FromDateTime(Now);
    }
}
