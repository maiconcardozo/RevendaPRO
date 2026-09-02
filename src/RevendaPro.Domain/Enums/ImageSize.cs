namespace RevendaPro.Domain.Enums
{
    /// <summary>
    /// The renditions kept for every photo. The screen asks for the smallest one that fits.
    ///
    /// The number is the longest side in pixels, and the aspect ratio is preserved. An image
    /// smaller than a target is kept as it is: enlarging invents detail that was never there
    /// and costs bytes to do it.
    /// </summary>
    public enum ImageSize
    {
        /// <summary>List and gallery strip.</summary>
        Thumbnail = 320,

        /// <summary>Card in the listing and the advertisement gallery.</summary>
        Card = 800,

        /// <summary>What opens when somebody wants to look closely.</summary>
        Full = 1600
    }
}
