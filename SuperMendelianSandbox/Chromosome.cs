using System;
using System.Collections.Generic;
using System.Linq;

namespace SMS
{
    class Chromosome
    {
        public List<GeneLocus> GeneLocusList;
        public string ChromosomeName;
        public string HomologousPairName;

        public Chromosome(string CName, string PName)
        {
            this.ChromosomeName = CName;
            this.HomologousPairName = PName;
            this.GeneLocusList = new List<GeneLocus>();
        }

        //Clone a Chromsome
        public Chromosome(Chromosome Old)
        {
            this.ChromosomeName = Old.ChromosomeName;
            this.HomologousPairName = Old.HomologousPairName;
            this.GeneLocusList = new List<GeneLocus>();

            foreach (GeneLocus OldGL in Old.GeneLocusList)
            {
                GeneLocus NewGL = new GeneLocus(OldGL);
                GeneLocusList.Add(NewGL);
            }
        }

        //New Chromosome in Meiosis (more complex DRIVE, with  gRNA checker)
        public Chromosome(Chromosome HomChrom1, Chromosome HomChrom2, Organism parent)
        {
            int Cas9level = parent.GetTransgeneLevel("transgene_Cas9");
            int RCD1RgRNAlevel = parent.GetTransgeneLevel("transgene_RCD1R_gRNA");
           

            if (HomChrom1.HomologousPairName != HomChrom2.HomologousPairName)
            { throw new System.ArgumentException("Not homologous Chromosomes", "warning"); }


            if (HomChrom1.HomologousPairName == "Sex")
            {
                if (Simulation.random.Next(0, 2) != 0)
                {
                    this.ChromosomeName = HomChrom1.ChromosomeName;
                    this.HomologousPairName = HomChrom1.HomologousPairName;
                    this.GeneLocusList = new List<GeneLocus>();

                    foreach (GeneLocus OldGL in HomChrom1.GeneLocusList)
                    {
                        GeneLocus NewGL = new GeneLocus(OldGL);
                        GeneLocusList.Add(NewGL);
                    }
                }
                else
                {
                    this.ChromosomeName = HomChrom2.ChromosomeName;
                    this.HomologousPairName = HomChrom2.HomologousPairName;
                    this.GeneLocusList = new List<GeneLocus>();

                    foreach (GeneLocus OldGL in HomChrom2.GeneLocusList)
                    {
                        GeneLocus NewGL = new GeneLocus(OldGL);
                        GeneLocusList.Add(NewGL);
                    }

                }
            }
            else
            {
                this.ChromosomeName = HomChrom1.ChromosomeName;
                this.HomologousPairName = HomChrom1.HomologousPairName;

                Chromosome HC1 = new Chromosome(HomChrom1);
                Chromosome HC2 = new Chromosome(HomChrom2);

                #region homing at all loci
                if (Cas9level > 0)
                {
                    for (var i = 0; i < HC1.GeneLocusList.Count; i++)
                    {
                        if (HC1.GeneLocusList[i].GeneName == HC2.GeneLocusList[i].GeneName)
                        {
                            if (HC1.GeneLocusList[i].GeneName == "RCD1R" && RCD1RgRNAlevel > 0)
                            {
                                if (HC1.GeneLocusList[i].AlleleName == "WT")
                                {
                                    if (Cas9level >= Simulation.random.Next(0, 101))
                                    {
                                        dynamic Hom_Repair = 0;
                                        dynamic Cons = 0;

                                        HC2.GeneLocusList[i].Traits.TryGetValue("Hom_Repair", out Hom_Repair);
                                        HC1.GeneLocusList[i].Traits.TryGetValue("Conservation", out Cons);

                                        if (HC2.GeneLocusList[i].AlleleName == "WT")
                                        { Hom_Repair = 1F; }

                                        if (Hom_Repair >= (float)Simulation.random.NextDouble())
                                        {
                                            HC1.GeneLocusList[i].AlleleName = HC2.GeneLocusList[i].AlleleName;
                                            HC1.GeneLocusList[i].InheritTraits(HC2.GeneLocusList[i]);
                                        }
                                        else
                                        {
                                            if (Cons >= (float)Simulation.random.NextDouble())
                                                HC1.GeneLocusList[i].AlleleName = "R2";
                                            else
                                                HC1.GeneLocusList[i].AlleleName = "R1";
                                        }
                                    }
                                }
                            }
                        }
                    }
                    ///====================
                    for (var i = 0; i < HC2.GeneLocusList.Count; i++)
                    {
                        if (HC1.GeneLocusList[i].GeneName == HC2.GeneLocusList[i].GeneName)
                        {
                            if (HC2.GeneLocusList[i].GeneName == "RCD1R" && RCD1RgRNAlevel > 0) 
                            {
                                if (HC2.GeneLocusList[i].AlleleName == "WT")
                                {
                                    if (Cas9level >= Simulation.random.Next(0, 101))
                                    {
                                        dynamic Hom_Repair = 0;
                                        dynamic Cons = 0;

                                        HC1.GeneLocusList[i].Traits.TryGetValue("Hom_Repair", out Hom_Repair);
                                        HC2.GeneLocusList[i].Traits.TryGetValue("Conservation", out Cons);

                                        if (HC1.GeneLocusList[i].AlleleName == "WT")
                                        { Hom_Repair = 1F; }

                                        if (Hom_Repair >= (float)Simulation.random.NextDouble())
                                        {
                                            HC2.GeneLocusList[i].AlleleName = HC1.GeneLocusList[i].AlleleName;
                                            HC2.GeneLocusList[i].InheritTraits(HC1.GeneLocusList[i]);
                                        }
                                        else
                                        {
                                            if (Cons >= (float)Simulation.random.NextDouble())
                                                HC2.GeneLocusList[i].AlleleName = "R2";
                                            else
                                                HC2.GeneLocusList[i].AlleleName = "R1";
                                        }
                                    }
                                }
                            }
                        }
                    }


                }
                #endregion

                this.GeneLocusList = new List<GeneLocus>();

                #region recombining the two homologous chroms to create new chrom

                for (var i = 0; i < HC1.GeneLocusList.Count; i++)
                {
                    if (Simulation.random.Next(0, 2) != 0)
                    {
                        this.GeneLocusList.Add(new GeneLocus(HC1.GeneLocusList[i]));
                    }
                    else
                    {
                        this.GeneLocusList.Add(new GeneLocus(HC2.GeneLocusList[i]));
                    }
                }
            }
            #endregion

        }

    }

}
