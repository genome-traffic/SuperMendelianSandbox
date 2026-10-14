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
        /// Human-readable identifier for this environment, included in CSV output
        /// to distinguish data from different environments.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// List of all populations in this environment. Each population is an independent
        /// breeding unit with its own carrying capacity. Populations are indexed by
        /// position in this list (0, 1, 2, ...).
        /// </summary>
        public List<Population> Populations
        { get; set; }

        /// <summary>
        /// Migration matrix: Migration[i,j] is the per-generation probability that any
        /// individual organism migrates from population i to population j.
        /// Sized 100x100 to support up to 100 populations. Only the upper triangle
        /// (j > i) is read during MigrateAll, so entry [i,j] always describes movement
        /// from the lower-numbered to the higher-numbered population.
        /// </summary>
        public float[,] Migration = new float[100, 100];

        /// <summary>
        /// When true, individuals move only from lower-numbered to higher-numbered
        /// populations — left to right along the chain, matching the arrows drawn in
        /// the results viewer. Nothing ever flows back upstream.
        ///
        /// The consequence worth knowing: population 0 receives no immigrants at all,
        /// so once a drive has eliminated it, it cannot be reseeded from its neighbour.
        /// Downstream populations likewise can only ever be reseeded from their left.
        ///
        /// When false, migration is bidirectional and symmetric: the same rate applies
        /// to movement in both directions along each connected pair.
        /// </summary>
        public bool OneWayMigration = true;

        /// <summary>
        /// When true, migrants are propagules rather than departures: the destination
        /// gains a copy of each migrant and the source keeps its own individual, so
        /// emigration never shrinks the population of origin. Gene flow then represents
        /// dispersal out of a population that is continuously replenished, rather than
        /// a physical transfer of bodies between patches.
        ///
        /// Note that the destination really does grow. Density regulation only acts at
        /// the egg stage, so between migration and the next round of reproduction a
        /// receiving population carries its carrying capacity plus that generation's
        /// immigrants, and the adult census in the output reflects that.
        ///
        /// When false, migrants are removed from the source as they leave, so a pair of
        /// connected populations conserves individuals between them.
        /// </summary>
        public bool NoLossMigration = true;


        /// <summary>
        /// Default constructor: creates an empty environment with no populations.
        /// </summary>
        public Environ()
        {
            this.Name = "";
            this.Populations = new List<Population>();
        }

        /// <summary>
        /// Creates an environment with a specified number of identical wild-type
        /// populations. Each population starts with the same size and carrying capacity,
        /// containing a 50/50 mix of WT males and females.
        /// </summary>
        /// <param name="name">Human-readable name for this environment (e.g., "Africa").</param>
        /// <param name="popnumber">Number of populations to create (minimum 1).</param>
        /// <param name="popsize">Initial size of each population (total individuals).</param>
        /// <param name="cap">Carrying capacity for each population.</param>
        public Environ(string name, int popnumber, int popsize, int cap)
		{
            this.Name = name;

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
        /// SingleMigration for each pair with a non-zero migration rate, so pairs are
        /// always visited with the lower-numbered population first.
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

                    //Console.WriteLine("Migration between populations " + p1.ToString() + " and " + p2.ToString() + "!");
                    SingleMigration(this.Populations[p1], this.Populations[p2], p1, p2);

                }
            }

        }

        /// <summary>
        /// Performs migration between two connected populations in a single step.
        /// One is always the lower-numbered population, so One → Two is the downstream
        /// (left to right) direction.
        ///
        /// The downstream flow always happens. The upstream flow Two → One happens only
        /// when OneWayMigration is false. Both directions use the same rate.
        ///
        /// Migrants are gathered into a holding pool and then handed to the destination
        /// via AddToPopulation, which deep-clones each organism. Under NoLossMigration
        /// the source keeps its own copy, so the two populations end up with genetically
        /// identical but independent individuals.
        /// </summary>
        /// <param name="One">The lower-numbered (upstream) population.</param>
        /// <param name="Two">The higher-numbered (downstream) population.</param>
        /// <param name="pop1">Index of the first population (for migration matrix lookup).</param>
        /// <param name="pop2">Index of the second population (for migration matrix lookup).</param>
        public void SingleMigration(Population One, Population Two, int pop1, int pop2)
        {
            float rate = this.Migration[pop1, pop2];

            Population ForwardPop = new Population();   // Organisms moving from One → Two
            CollectMigrants(One, ForwardPop, rate);
            Two.AddToPopulation(ForwardPop);

            if (this.OneWayMigration)
                return;

            Population RevPop = new Population();       // Organisms moving from Two → One
            CollectMigrants(Two, RevPop, rate);
            One.AddToPopulation(RevPop);
        }

        /// <summary>
        /// Draws migrants out of a source population into a holding pool: each adult
        /// leaves independently with probability <paramref name="rate"/>.
        ///
        /// Under NoLossMigration the organism is only referenced by the pool and stays
        /// in the source; the destination will clone it, so the two copies diverge from
        /// then on. Otherwise it is removed from the source as it leaves, and the o--
        /// after RemoveAt compensates for the index shift during forward iteration.
        /// </summary>
        /// <param name="Source">Population the migrants leave from.</param>
        /// <param name="Pool">Holding pool that receives them.</param>
        /// <param name="rate">Per-individual probability of migrating this generation.</param>
        void CollectMigrants(Population Source, Population Pool, float rate)
        {
            for (int o = 0; o < Source.Adults.Count; o++)
            {
                if (rate >= (float)Shuffle.random.NextDouble())
                {
                    Pool.Adults.Add(Source.Adults[o]);

                    if (!this.NoLossMigration)
                        Source.Adults.RemoveAt(o--);
                }
            }
        }

	}
}

