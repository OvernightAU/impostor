using System;

public static class RandomExtensions
{
    public static void FillBytes(this Random random, byte[] buffer)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        random.NextBytes(buffer);
    }
}
