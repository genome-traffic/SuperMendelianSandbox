using System;
using SMS;
using System.Collections.Generic;

namespace SMS
{
	class Environ
	{

        public List<Population> Populations
        { get; set; }

        public float[,] Migration = new float[100, 100];
        //Array.Clear(Migration, 0, Migration.Length);


        public Environ()
        {
        }

        //predefined environw with WT populations
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

        public void DefineMigration(int pop1, int pop2, float likelyhood)
        {
            this.Migration[pop1, pop2] = likelyhood;
        }



        public void MigrateAll()
        {
            
            for (int p1 = 0; p1 < this.Populations.Count; p1++)
            {
                for (int p2 = 0; p2 < this.Populations.Count; p2++)
                {
                    if (p1 == p2)
                        continue;

                    if (p2 < p1)
                        continue;

                    if (this.Migration[p1, p2] == 0)
                        continue;

                    Console.WriteLine("Migration between populations " + p1.ToString() + " and " + p2.ToString() + "!");
                    SingleMigration(this.Populations[p1], this.Populations[p2], p1, p2);

                }
            }
            
        }

        public void SingleMigration(Population One, Population Two, int pop1, int pop2)
        {
            Population ForwadPop = new Population();
            Population RevPop = new Population();

            //Console.WriteLine(((float)Shuffle.random.NextDouble()).ToString());


            for (int o = 0; o < One.Adults.Count; o++)
            {
                if (this.Migration[pop1, pop2] >= (float)Shuffle.random.NextDouble())
                {
                    ForwadPop.Adults.Add(One.Adults[o]);
                    One.Adults.RemoveAt(o--);
                }
            }


            for (int o = 0; o < Two.Adults.Count; o++)
            {
                if (this.Migration[pop1, pop2] >= (float)Shuffle.random.NextDouble())
                {
                    RevPop.Adults.Add(Two.Adults[o]);
                    Two.Adults.RemoveAt(o--);
                }
            }

            //Console.WriteLine(ForwadPop.Adults.Count.ToString());
            //Console.WriteLine(RevPop.Adults.Count.ToString());

            Two.AddToPopulation(ForwadPop);
            One.AddToPopulation(RevPop);

        }

	}
}

