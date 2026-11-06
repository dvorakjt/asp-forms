using System;

namespace FormsSandbox.Prototypes.Transpose
{
    public static class Program
    {
        public static void Main()
        {
            for (var index = 0; index < SharedQuantity.Samples.Length; index++)
            {
                Console.WriteLine(SharedQuantity.Evaluate(SharedQuantity.Samples[index],
                                                         SharedQuantity.Limits[index]).Snapshot());
            }
        }
    }
}