using System;
using SMS;
using System.Collections.Generic;

namespace SMS
{
    /// <summary>
    /// Represents the spatial environment containing multiple populations connected by
    /// migration. Models a metapopulation structure where gene drive spread depends on
    /// both within-population dynamics and between-population connectivity.
    ///
    /// The environment holds:
    ///   - A list of Population objects (each an independent breeding unit).
    ///   - A migration matrix defining per-generation, per-individual probability of
    ///     moving between any pair of populations.
    ///
    /// Migration is bidirectional and symmetric (uses the same rate for both directions
    /// of a given population pair). Only upper-triangle entries of the migration matrix
    /// are used (p2 > p1), avoiding double-counting.
    /// </summary>
	class Environ
	{

        /// <summary>
        /// List of all populations in this environment. Each population is an independent
        /// breeding unit with its own carrying capacity. Populations are indexed by
        /// position in this list (0, 1, 2, ...).
        /// </summary>
        public List<Population> Populations
        { get; set; }

        /// <summary>
        /// Migration matrix: Migration[i,j] is the per-generation probability that any
        /// individual organism migrates from population i to population j (and vice versa).
        /// Sized 100x100 to support up to 100 populations. Only the upper triangle
        /// (j > i) is read during MigrateAll; entries are symmetric.
        /// </summary>
        public float[,] Migration = new float[100, 100];


        /// <summary>
        /// Default constructor: creates an empty environment with no populations.
        /// </summary>
        public Environ()
        {
        }

        /// <summary>
        /// Creates an environment with a specified number of identical wild-type
        /// populations. Each population starts with the same size and carrying capacity,
        /// containing a 50/50 mix of WT males and females.
        /// </summary>
        /// <param name="popnumber">Number of populations to create (minimum 1).</param>
        /// <param name="popsize">Initial size of each population (total individuals).</param>
        /// <param name="cap">Carrying capacity for each population.</param>
        public Environ(int popnumber, int popsize, int cap)
		{
            if (popnumber < 1)
            { popnumber = 1; }

            this.Populations = new List<Population>();

            for (int i = 0; i < popnumber; i++)
            {
                this.Populations.Add(new Population(popsize, cap));
            }
        }

        /// <summary>
        /// Sets the migration rate between two populations. The rate is used symmetrically
        /// (same probability for movement in both directions). Only needs to be set for
        /// pairs where pop1 < pop2, as MigrateAll only reads the upper triangle.
        /// </summary>
        /// <param name="pop1">Index of the first population.</param>
        /// <param name="pop2">Index of the second population.</param>
        /// <param name="likelyhood">Per-individual, per-generation migration probability (0–1).</param>
        public void DefineMigration(int pop1, int pop2, float likelyhood)
        {
            this.Migration[pop1, pop2] = likelyhood;
        }



        /// <summary>
        /// Executes migration between all connected population pairs for one generation.
        /// Iterates over the upper triangle of the migration matrix (p2 > p1) and calls
        /// SingleMigration for each pair with a non-zero migration rate.
        /// </summary>
        public void MigrateAll()
        {

            for (int p1 = 0; p1 < this.Populations.Count; p1++)
            {
                for (int p2 = 0; p2 < this.Populations.Count; p2++)
                {
                    if (p1 == p2)
                        continue;       // Skip self-migration

                    if (p2 < p1)
                        continue;       // Only process upper triangle (avoid double-counting)

                    if (this.Migration[p1, p2] == 0)
                        continue;       // Skip unconnected populations

                    Console.WriteLine("Migration between populations " + p1.ToString() + " and " + p2.ToString() + "!");
                    SingleMigration(this.Populations[p1], this.Populations[p2], p1, p2);

                }
            }

        }

        /// <summary>
        /// Performs bilateral migration between two populations in a single step.
        ///
        /// For each adult in Population One: with probability Migration[pop1,pop2],
        /// the organism is removed from One and added to a forward-migration pool.
        /// Similarly for Population Two into a reverse-migration pool.
        ///
        /// After processing both populations, the forward pool is added to Two and
        /// the reverse pool is added to One (via AddToPopulation, which deep-clones).
        ///
        /// Note: The o-- after RemoveAt compensates for the index shift when removing
        /// elements during forward iteration.
        /// </summary>
        /// <param name="One">First population.</param>
        /// <param name="Two">Second population.</param>
        /// <param name="pop1">Index of first population (for migration matrix lookup).</param>
        /// <param name="pop2">Index of second population (for migration matrix lookup).</param>
        public void SingleMigration(Population One, Population Two, int pop1, int pop2)
        {
            Population ForwadPop = new Population();   // Organisms moving from One → Two
            Population RevPop = new Population();       // Organisms moving from Two → One


            // Stochastically select migrants from Population One → Two
            for (int o = 0; o < One.Adults.Count; o++)
            {
                if (this.Migration[pop1, pop2] >= (float)Shuffle.random.NextDouble())
                {
                    ForwadPop.Adults.Add(One.Adults[o]);
                    One.Adults.RemoveAt(o--);  // Remove migrant; adjust index
                }
            }

            // Stochastically select migrants from Population Two → One
            for (int o = 0; o < Two.Adults.Count; o++)
            {
                if (this.Migration[pop1, pop2] >= (float)Shuffle.random.NextDouble())
                {
                    RevPop.Adults.Add(Two.Adults[o]);
                    Two.Adults.RemoveAt(o--);  // Remove migrant; adjust index
                }
            }

            // Add migrants to their destination populations
            Two.AddToPopulation(ForwadPop);
            One.AddToPopulation(RevPop);

        }

	}
}

