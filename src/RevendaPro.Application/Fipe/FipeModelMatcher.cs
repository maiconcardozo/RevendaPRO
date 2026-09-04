using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RevendaPro.Domain.Entities;
using RevendaPro.Domain.Enums;
using RevendaPro.Domain.ValueObjects;

namespace RevendaPro.Application.Fipe
{
    /// <summary>
    /// Narrows the models of a brand down to the ones that can be this vehicle.
    ///
    /// <b>It eliminates, and never guesses.</b> The reference table has no text search, and what
    /// it calls a "model" is the whole trim — the Jeep brand alone answers 110 of them, 32 of
    /// which carry the word <c>Renegade</c>. Turning "Jeep / Renegade / 1.8 Longitude" into one
    /// of those is a matter of throwing away what cannot be it, and then showing whoever is
    /// looking what survived.
    ///
    /// Two trims of the same car are two different prices — often tens of thousands apart — so
    /// this class deliberately stops at "these are the candidates". Choosing between two prices
    /// belongs to the person, which is the same line the whole M11 follows: the system suggests
    /// by presence, and money is decided by whoever knows the car.
    ///
    /// Nothing here touches the network: it receives the lists the source already answered, and
    /// gives back an order. That is what makes the rule testable against real table names.
    /// </summary>
    public static class FipeModelMatcher
    {
/// <summary>
        /// How the table marks a car that shifts by itself. <c>Mec.</c> has no place here on
        /// purpose: the stick is written by saying nothing.
        /// </summary>
        private static readonly string[] Automatics =
            ["aut", "automatico", "cvt", "tiptronic", "dsg", "dct"];

        /// <summary>
        /// O câmbio automatizado da VW, escrito como duas palavras.
        ///
        /// Vale como automático porque é: o I Motion troca sozinho, e um carro de câmbio manual
        /// jamais é um deles. O prefixo para em <c>i moti</c> de propósito, porque a tabela
        /// abrevia — <c>Gol 1.6 I MOTI.Power/Highli T.Flex 8V 4p</c> é a mesma coisa.
        ///
        /// A palavra sozinha ficaria perigosa: <c>4MOTION</c> é tração nas quatro rodas, e não
        /// tem nada a ver com câmbio.
        /// </summary>
        private const string AutomatedPhrase = "i moti";

        /// <summary>Fuel words as the table writes them, by the fuel the vehicle carries.</summary>
        private static readonly Dictionary<FuelType, string> FuelWords = new()
        {
            [FuelType.Flex] = "flex",
            [FuelType.Gasoline] = "gasolina",
            [FuelType.Ethanol] = "alcool",
            [FuelType.Diesel] = "diesel",
            [FuelType.Hybrid] = "hibrido",
            [FuelType.Electric] = "eletrico",
        };

        /// <summary>
        /// The brand of the table that answers for this brand.
        ///
        /// The match is loose on purpose, and in one direction at a time: the table writes
        /// <c>GM - Chevrolet</c> and <c>VW - VolksWagen</c>, while a vehicle is registered as
        /// <c>Chevrolet</c> and <c>Volkswagen</c>. An exact name wins first, so a brand that
        /// happens to be contained in another never steals the answer.
        /// </summary>
        /// <param name="brands">Brands the table prices.</param>
        /// <param name="brand">Brand as the vehicle carries it.</param>
        /// <returns>The brand of the table, or null when nothing answers for it.</returns>
        public static FipeNamed? FindBrand(IReadOnlyList<FipeNamed> brands, string brand)
        {
            ArgumentNullException.ThrowIfNull(brands);

            var wanted = Plain(brand);

            if (wanted.Length == 0)
            {
                return null;
            }

            return brands.FirstOrDefault(option => Plain(option.Name) == wanted)
                ?? brands.FirstOrDefault(option => Plain(option.Name).Contains(wanted, StringComparison.Ordinal))
                ?? brands.FirstOrDefault(option => wanted.Contains(Plain(option.Name), StringComparison.Ordinal));
        }

        /// <summary>
        /// The models that can be this vehicle, best first.
        ///
        /// The name of the model is required as a <b>whole word</b>, which is what keeps
        /// <c>Gol</c> out of <c>Golf</c>. Everything after that only scores: each word of the
        /// version, the gearbox — the table writes <c>Aut.</c> and <c>Mec.</c> — and the fuel.
        /// Only the highest scoring group survives.
        ///
        /// <b>It steps back rather than answering nothing.</b> A version nobody wrote, or written
        /// in a way the table never uses, leaves every candidate tied at zero — and a tie at zero
        /// returns all of them. Offering four is worth more than offering none.
        /// </summary>
        /// <param name="models">Models of the brand, as the table answered.</param>
        /// <param name="vehicle">The vehicle being matched.</param>
        /// <returns>The surviving models, ordered by name. Empty only when the name matches nothing.</returns>
        public static IReadOnlyList<FipeNamed> Narrow(IReadOnlyList<FipeNamed> models, Vehicle vehicle)
        {
            var tiers = Ranked(models, vehicle);

            return tiers.Count == 0 ? [] : tiers[0];
        }

        /// <summary>
        /// Os mesmos modelos em camadas, da que mais repete o carro para a que menos repete.
        ///
        /// <b>Existe porque o nome do modelo muda entre gerações, e o ano é quem sabe disso.</b>
        /// A tabela lista o Gol 1.6 MSI de 2019 a 2022; o mesmo motor, num carro 2015, ela
        /// escreve como <c>Gol Trendline 1.6 T.Flex 8V 5p</c> — o motor vira acabamento, e a
        /// palavra "MSI" simplesmente não existe naquela geração.
        ///
        /// Parar na camada de maior pontuação, então, ofereceria um modelo que a tabela nunca
        /// precificou no ano deste carro. Quem chama percorre as camadas de cima para baixo e
        /// desce enquanto o ano não aparecer.
        /// </summary>
        /// <param name="models">Models of the brand, as the table answered.</param>
        /// <param name="vehicle">The vehicle being matched.</param>
        /// <returns>As camadas, da melhor para a pior. Vazio quando o nome casa com nada.</returns>
        public static IReadOnlyList<IReadOnlyList<FipeNamed>> Ranked(
            IReadOnlyList<FipeNamed> models,
            Vehicle vehicle)
        {
            ArgumentNullException.ThrowIfNull(models);
            ArgumentNullException.ThrowIfNull(vehicle);

            var named = ByName(models, vehicle.Model);

            return
            [
                .. named
                    .GroupBy(model => Score(model.Name, vehicle))
                    .OrderByDescending(group => group.Key)
                    .Select(group => (IReadOnlyList<FipeNamed>)
                    [
                        .. group.OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase),
                    ]),
            ];
        }

        /// <summary>
        /// The year and fuel rows of a model that answer for the year of this vehicle.
        ///
        /// It is the strongest elimination there is — a trim the table never priced in 2020
        /// simply cannot be a 2020 car — and also the most expensive, because it costs one call
        /// per candidate. Who pays that call decides how many candidates are worth checking.
        /// </summary>
        /// <param name="years">Year and fuel rows of one model.</param>
        /// <param name="modelYear">Model year of the vehicle.</param>
        /// <returns>The rows of that year.</returns>
        public static IReadOnlyList<FipeYearOption> YearsOf(
            IReadOnlyList<FipeYearOption> years,
            short modelYear)
        {
            ArgumentNullException.ThrowIfNull(years);

            return [.. years.Where(option => option.ModelYear == modelYear)];
        }

        /// <summary>Models carrying the name as a whole word, with a looser second pass.</summary>
        private static IReadOnlyList<FipeNamed> ByName(IReadOnlyList<FipeNamed> models, string model)
        {
            var wanted = Plain(model);

            if (wanted.Length == 0)
            {
                return [];
            }

            var whole = models.Where(option => HasWord(option.Name, wanted)).ToList();

            if (whole.Count > 0)
            {
                return whole;
            }

            // "HB20" and "HB 20" are the same car written twice. The second pass drops every
            // separator from both sides before comparing, and it only runs when the honest
            // match found nothing.
            var squeezed = Squeeze(wanted);

            return [.. models.Where(option =>
                Squeeze(Plain(option.Name)).Contains(squeezed, StringComparison.Ordinal))];
        }

        /// <summary>How much of the vehicle a model name repeats.</summary>
        private static int Score(string name, Vehicle vehicle)
        {
            var plain = Plain(name);
            var score = 0;

            // The version is the strongest signal after the name: "1.8" and "Longitude" are
            // exactly what separates one Renegade from the other thirty-one.
            foreach (var term in Terms(vehicle.Version))
            {
                if (plain.Contains(term, StringComparison.Ordinal))
                {
                    score += 2;
                }
            }

            score += GearboxScore(plain, vehicle.Transmission);

            if (FuelWords.TryGetValue(vehicle.FuelType, out var fuel)
                && plain.Contains(fuel, StringComparison.Ordinal))
            {
                score++;
            }

            return score;
        }

/// <summary>
        /// How well the gearbox of the name fits the gearbox of the car.
        ///
        /// <b>The table marks the automatic and leaves the stick unmarked.</b> Of the two rows
        /// the table prices for a Gol 1.6 MSI, one reads <c>Flex 16V 5p Aut.</c> and the other
        /// reads <c>Flex 8V 5p</c> and says nothing — looking for the word <c>Mec.</c> in the
        /// second one would find nothing and separate neither.
        ///
        /// So a manual car scores on the <b>absence</b> of the automatic mark. Automated and CVT
        /// both count as automatic, which is how the table writes them.
        /// </summary>
        private static int GearboxScore(string plain, TransmissionType transmission)
        {
            var automatic = Automatics.Any(word => HasWordStart(plain, word))
                || HasWordStart(plain, AutomatedPhrase);

            return transmission switch
            {
                TransmissionType.Manual => automatic ? 0 : 1,
                TransmissionType.Automatic
                    or TransmissionType.AutomatedManual
                    or TransmissionType.Cvt => automatic ? 1 : 0,
                _ => 0,
            };
        }

        /// <summary>Words of the version worth looking for. One letter says nothing.</summary>
        private static IEnumerable<string> Terms(string? version) =>
            Plain(version ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length > 1)
                .Distinct(StringComparer.Ordinal);

        /// <summary>Whether the name carries the term surrounded by anything but a letter or digit.</summary>
        private static bool HasWord(string name, string term) =>
            Regex.IsMatch(
                Plain(name),
                $"(^|[^a-z0-9]){Regex.Escape(term)}([^a-z0-9]|$)",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));

        /// <summary>
        /// Whether a word of the name starts with the term.
        ///
        /// It is looser than a whole word on the right side, and strict on the left: it has to
        /// catch <c>Aut.</c> and <c>Aut.4p</c>, and stay out of the middle of another word.
        /// </summary>
        private static bool HasWordStart(string plain, string term) =>
            Regex.IsMatch(
                plain,
                $"(^|[^a-z0-9]){Regex.Escape(term)}",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));

        /// <summary>Lower case, without accents, and with every separator turned into a space.</summary>
        private static string Plain(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(char.IsLetterOrDigit(character) || character == '.' ? character : ' ');
            }

            return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>The same text with every separator gone, for the looser second pass.</summary>
        private static string Squeeze(string value) =>
            new([.. value.Where(char.IsLetterOrDigit)]);
    }
}
