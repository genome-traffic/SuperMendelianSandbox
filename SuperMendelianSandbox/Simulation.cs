using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.IO;


namespace SMS
{
    class Simulation
    {
        public List<Organism> Adults = new List<Organism>();
        public List<Organism> Eggs = new List<Organism>();
        public static Random random = new Random();

    /*-------------------- Simulation Parameters ---------------------------------*/

        public int Generations = 10;
        public int Iterations = 50;

        public int PopulationCap = 200;
        public float Mortality = 0.1f;
        public static float MaternalHDRReduction = 0.05F;
        public int GlobalEggsPerFemale = 50;
        public int Sample = 48;

        public bool ApplyIntervention = false;
        public int StartingNumberOfWTFemales = 250;
        public int StartingNumberOfWTMales = 250;
        public int StartIntervention = 2;
        public int EndIntervention = 2;
        public int InterventionReleaseNumber = 125;

        string[] Track = {"CP","Cas9_helper"};

        public static string[,] Target_cognate_gRNA = { { "CP", "gRNA_CP" }, { "Transformer", "gRNA_tra" } };

        /*------------------------------- The Simulation ---------------------------------------------*/

        public void Simulate()
        { 
            string pathdesktop = (string)Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            pathdesktop = pathdesktop + "/model";
            string pathString = System.IO.Path.Combine(pathdesktop, "MMCP.csv");
            Console.WriteLine("Writing output to: " + pathString);
            File.Create(pathString).Dispose();

            Console.WriteLine("Simulation Starts.");

            using (var stream = File.OpenWrite(pathString))
            using (var Fwriter = new StreamWriter(stream))
            {
                // THE ACTUAL SIMULATION
                for (int cIterations = 1; cIterations <= Iterations; cIterations++)
                {
                    Console.WriteLine("Iteration " + cIterations + " out of " + Iterations);
                    Adults.Clear();
                    Eggs.Clear();

                    if (ApplyIntervention)
                        Populate_with_WT();
                    else
                        Populate_with_Setup();

                    Shuffle.ShuffleList(Adults);

                    for (int cGenerations = 1; cGenerations <= Generations; cGenerations++)
                    {
                        if (ApplyIntervention)
                        {
                            if ((cGenerations >= StartIntervention) && (cGenerations <= EndIntervention))
                            {
                                Intervention();
                            }
                        }

                        #region maternal effects
                        foreach (Organism OM in Adults)
                        {
                            // implement here
                            if (Simulation.random.Next(0, 2) != 0)
                            {
                                OM.SwapChromLists();
                            }

                            OM.EmbryonicCas9Activity();

                            OM.MaternalFactors.Clear();                            
                        }
                        #endregion

                        #region output adult data to file

                        //------------------------ Genotypes -------

                        List<string> Genotypes = new List<string>();

                         foreach (Organism O in Adults)
                         {
                             foreach (string s in Track)
                             {
                                Genotypes.Add(s + "," + O.GetGenotype(s));
                             }
                         }

                        var queryG = Genotypes.GroupBy(s => s)
                           .Select(g => new { Name = g.Key, Count = g.Count() });

                        foreach (var result in queryG)
                        {
                          Fwriter.WriteLine("{0},{1},{2},{3},all", cIterations, cGenerations, result.Name, result.Count);
                        }

                        Genotypes.Clear();

                        int cSample = Sample;
                        foreach (Organism O in Adults)
                        {
                            if (cSample > 0)
                            {
                                foreach (string s in Track)
                                {
                                    Genotypes.Add(s + "," + O.GetGenotype(s));
                                }
                                cSample--;
                            }
                        }

                        var queryGs = Genotypes.GroupBy(s => s)
                           .Select(g => new { Name = g.Key, Count = g.Count() });

                        foreach (var result in queryGs)
                        {
                            Fwriter.WriteLine("{0},{1},{2},{3},sample", cIterations, cGenerations, result.Name, result.Count);
                        }

                        //------------------------- Sex -----------
                        int numberofallmales = 0;
                        int numberofallfemales = 0;
                        foreach (Organism O in Adults)
                        {
                            if (O.GetSex() == "female")
                                numberofallfemales++;
                            else
                                numberofallmales++;
                        }
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex", "Males", "NA", numberofallmales, "all");
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex", "Females", "NA", numberofallfemales, "all");

                        //------------------------- Sex Karyotype -----------
                        int numberofXX = 0;
                        int numberofXY = 0;
                        foreach (Organism O in Adults)
                        {

                            switch (O.GetSexChromKaryo())
                            {
                                case "XX":
                                    {
                                        numberofXX++;
                                        break;
                                    }
                                case "XY":
                                    {
                                        numberofXY++;
                                        break;
                                    }
                                case "YX":
                                    {
                                        numberofXY++;
                                        break;
                                    }
                                default:
                                    {
                                        Console.WriteLine(O.GetSexChromKaryo() + " should not exist!");
                                        break;
                                    }
                            }

                        }
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex_Karyotype", "XX", "NA", numberofXX, "all");
                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Sex_Karyotype", "XY", "NA", numberofXY, "all");



                        #endregion

                        #region Cross all adults and return eggs for next generation

                        Shuffle.ShuffleList(Adults);
                        CrossAll();
                        Adults.Clear();
                        Shuffle.ShuffleList(Eggs);

                        int EggsToBeReturned = 0;

                        if (Eggs.Count <= PopulationCap)
                            EggsToBeReturned = Eggs.Count;
                        else
                            EggsToBeReturned = PopulationCap;

                        for (int na = 0; na < EggsToBeReturned; na++)
                        {
                            Adults.Add(new Organism(Eggs[na]));
                        }

                        Fwriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}", cIterations, cGenerations, "Eggs", "NA", "NA", Eggs.Count.ToString(), "all");

                        Eggs.Clear();

                        #endregion

                    }
                }
                // END OF SIMULATION

                Fwriter.Flush();
            }
        }

        //---------------------- Define Organisms, Genotypes and Starting Populations -----------------------------------------------------

        public void Populate_with_WT()
        {
            //for (int i = 0; i < StartingNumberOfWTFemales; i++)
            //{
            //    Adults.Add(new Organism(GenerateWTFemale()));
            //}
            //for (int i = 0; i < StartingNumberOfWTMales; i++)
            //{
            //    Adults.Add(new Organism(GenerateWTMale()));
            //}
        }

        public void Populate_with_Setup()
        {
            for (int i = 0; i < 60; i++)
            {
                Adults.Add(new Organism(Generate_Transhet_Female()));
            }
            for (int i = 0; i < 60; i++)
            {
                Adults.Add(new Organism(Generate_Transhet_Male()));
            }

            for (int i = 0; i < 40; i++)
            {
                Adults.Add(new Organism(GenerateCas9Female()));
            }
            for (int i = 0; i < 40; i++)
            {
                Adults.Add(new Organism(GenerateCas9Male()));
            }


        }

        public void Intervention()
        {
            for (int i = 0; i < InterventionReleaseNumber; i++)
            {
                //Adults.Add(new Organism(Generate_DriveMale()));
            }
        }

        public Organism GenerateCas9Female()
        {
            Organism WTFemale = new Organism();

            GeneLocus CPa = new GeneLocus("CP", 1, "WT");
            CPa.Traits.Add("Conservation", 0.95F);
            CPa.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus CPb = new GeneLocus("CP", 1, "WT");
            CPb.Traits.Add("Conservation", 0.95F);
            CPb.Traits.Add("Hom_Repair", 0.95F);

            GeneLocus Insertion_a = new GeneLocus("Cas9_helper", 1, "Transgene");
            Insertion_a.Traits.Add("Cas9", 0.95F);
            Insertion_a.Traits.Add("Cas9_maternal", 0F);
            Insertion_a.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus Insertion_b = new GeneLocus("Cas9_helper", 1, "WT");
    
            Chromosome ChromXa = new Chromosome("X", "Sex");
            Chromosome ChromXb = new Chromosome("X", "Sex");
            Chromosome Chrom2a = new Chromosome("2", "2");
            Chromosome Chrom2b = new Chromosome("2", "2");
            Chromosome Chrom3a = new Chromosome("3", "3");
            Chromosome Chrom3b = new Chromosome("3", "3");

            Chrom2a.GeneLocusList.Add(CPa);
            Chrom2b.GeneLocusList.Add(CPb);

            Chrom3a.GeneLocusList.Add(Insertion_a);
            Chrom3b.GeneLocusList.Add(Insertion_b);

            WTFemale.ChromosomeListA.Add(ChromXa);
            WTFemale.ChromosomeListB.Add(ChromXb);
            WTFemale.ChromosomeListA.Add(Chrom2a);
            WTFemale.ChromosomeListB.Add(Chrom2b);
            WTFemale.ChromosomeListA.Add(Chrom3a);
            WTFemale.ChromosomeListB.Add(Chrom3b);

            return WTFemale;
        }

        public Organism GenerateCas9Male()
        {
            Organism WTMale = new Organism(GenerateCas9Female());
            Chromosome ChromY = new Chromosome("Y", "Sex");
            GeneLocus MaleFactor = new GeneLocus("MaleDeterminingLocus", 1, "WT");
            ChromY.GeneLocusList.Add(MaleFactor);

            WTMale.ChromosomeListA[0] = ChromY;
            return WTMale;
        }

        public Organism Generate_Transhet_Female()
        {
            Organism THfemale = new Organism();

            GeneLocus CPa = new GeneLocus("CP", 1, "WT");
            CPa.Traits.Add("Conservation", 0.95F);
            CPa.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus CPb = new GeneLocus("CP", 1, "Transgene");
            CPb.Traits.Add("gRNA_CP", 1.0F);
            CPb.Traits.Add("Hom_Repair", 0.95F);
   
            GeneLocus Insertion_a = new GeneLocus("Cas9_helper", 1, "Transgene");
            Insertion_a.Traits.Add("Cas9", 0.95F);
            Insertion_a.Traits.Add("Cas9_maternal", 0F);
            Insertion_a.Traits.Add("Hom_Repair", 0.95F);
            GeneLocus Insertion_b = new GeneLocus("Cas9_helper", 1, "WT");

            Chromosome ChromXa = new Chromosome("X", "Sex");
            Chromosome ChromXb = new Chromosome("X", "Sex");
            Chromosome Chrom2a = new Chromosome("2", "2");
            Chromosome Chrom2b = new Chromosome("2", "2");
            Chromosome Chrom3a = new Chromosome("3", "3");
            Chromosome Chrom3b = new Chromosome("3", "3");

            Chrom2a.GeneLocusList.Add(CPa);
            Chrom2b.GeneLocusList.Add(CPb);

            Chrom3a.GeneLocusList.Add(Insertion_a);
            Chrom3b.GeneLocusList.Add(Insertion_b);

            THfemale.ChromosomeListA.Add(ChromXa);
            THfemale.ChromosomeListB.Add(ChromXb);
            THfemale.ChromosomeListA.Add(Chrom2a);
            THfemale.ChromosomeListB.Add(Chrom2b);
            THfemale.ChromosomeListA.Add(Chrom3a);
            THfemale.ChromosomeListB.Add(Chrom3b);

            return THfemale;
        }

        public Organism Generate_Transhet_Male()
        {

            Organism THMale = new Organism(Generate_Transhet_Female());
            Chromosome ChromY = new Chromosome("Y", "Sex");
            GeneLocus MaleFactor = new GeneLocus("MaleDeterminingLocus", 1, "WT");
            ChromY.GeneLocusList.Add(MaleFactor);

            THMale.ChromosomeListA[0] = ChromY;
            return THMale;

        }

        //----------------------- Simulation methods ----------------------------------------------------

        public void PerformCross(Organism Dad, Organism Mum, ref List<Organism> EggList)
        {
            int EggsPerFemale = GlobalEggsPerFemale;

            EggsPerFemale = (int)(EggsPerFemale * Dad.GetFertility() * Mum.GetFertility());

                for (int i = 0; i < EggsPerFemale; i++)
                {
                    EggList.Add(new Organism(Dad,Mum));
                }
        }
        public void CrossAll()
        {

            int EffectivePopulation = (int)((1 - Mortality) * PopulationCap);
  
            int numb;
            foreach (Organism F1 in Adults)
            {
                if (F1.GetSex() == "male")
                {
                    continue;
                }
                else
                {
                    for (int a = 0; a < EffectivePopulation; a++)
                    {
                        numb = random.Next(0, Adults.Count);
                        if (Adults[numb].GetSex() == "male")
                        {
                            PerformCross(Adults[numb], F1, ref Eggs);
                            break;
                        }
                    }
                }

            }

        }

    }
}
