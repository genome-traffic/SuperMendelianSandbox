using System;
using System.Collections.Generic;
using System.Linq;

namespace SMS
{
    /// <summary>
    /// Utility class providing randomization functionality used throughout the simulation.
    /// Houses the single shared Random instance to ensure consistent pseudo-random number
    /// generation across all stochastic processes (mating, recombination, gene drive activity,
    /// migration, etc.).
    /// </summary>
    public static class Shuffle
    {
        /// <summary>
        /// Shared random number generator used by all stochastic processes in the simulation.
        /// A single instance avoids seed collisions that occur when multiple Random objects
        /// are created in rapid succession.
        /// </summary>
        public static Random random = new Random();

        /// <summary>
        /// Performs an in-place Fisher-Yates shuffle on the given list, producing a
        /// uniformly random permutation. Used to randomize mating order, population
        /// lists, and other collections where unbiased randomization is required.
        /// </summary>
        /// <typeparam name="E">The element type of the list.</typeparam>
        /// <param name="list">The list to shuffle in place.</param>
        public static void ShuffleList<E>(IList<E> list)
        {
            if (list.Count > 1)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    E tmp = list[i];
                    int randomIndex = random.Next(i + 1);

                    //Swap elements
                    list[i] = list[randomIndex];
                    list[randomIndex] = tmp;
                }
            }
        }
    }
}
