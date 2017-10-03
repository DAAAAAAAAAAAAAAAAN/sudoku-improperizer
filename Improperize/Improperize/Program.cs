using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Improperize
{
    class Program
    {
        static Stopwatch timer = Stopwatch.StartNew();

        static string appTime()
        {
            return timer.Elapsed.TotalSeconds.ToString("0").PadLeft(5, ' ') + "s";
        }

        static string python (string cmd, string args)
        {
            return run_cmd(@"python.exe", cmd, args);
        }

        static string run_cmd(string file, string cmd, string args)
        {
            string output = "";
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = file;
            start.Arguments = string.Format("{0} {1}", cmd, args);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    output = reader.ReadToEnd();
                }
            }
            return output;
        }

        static List<string> loadFile (string file)
        {
            var lines = File.ReadAllLines(file).Select(a => a.Split(',')[0]);
            var csv = from line in lines
                      select (line.Split(',')[0]);
            return csv.ToList();
        }

        static List<string> findSolutions (int maxNumberOfSolutions)
        {
            var solutions = new List<string>();
            bool hasFoundSolution = true;
            int solutionCounter = 0;
            while (hasFoundSolution)
            {
                // solve
                var solverOutput = run_cmd(@"zchaff.exe", "", "WIP_sudoku.cnf");

                hasFoundSolution = !solverOutput.Contains(@"UNSAT");
                if (hasFoundSolution)
                {
                    solutionCounter++;
                    if (solutionCounter > maxNumberOfSolutions)
                    {
                        return new List<string>();
                    }

                    // edit solution file to remove garbage from zchaff
                    var lines = solverOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var solutionLine = lines[4];
                    var solution = solutionLine.Substring(0, solutionLine.IndexOf("Random"));

                    // write solution to file
                    File.WriteAllText(@"WIP_sudoku_solution.cnf", solution);

                    // convert solution to sudoku
                    python("cnf-to-sudoku.py", "-i WIP_sudoku_solution.cnf -o WIP_sudoku_solution.txt");

                    // read solution file
                    var solutionString = loadFile("WIP_sudoku_solution.txt")[0];

                    // exclude solution from being found again
                    var negation = "";
                    var literals = solution.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var literal in literals)
                    {
                        if (literal.Contains("-"))
                        {
                            negation += literal.Substring(literal.IndexOf('-')+1) + " ";
                        }
                        else
                        {
                            negation += "-" + literal + " ";
                        }
                    }
                    negation += "0";
                    var negationLines = new List<string>();
                    negationLines.Add(negation);
                    File.AppendAllLines("WIP_sudoku.cnf", negationLines);

                    solutions.Add(solutionString);
                }
            }
            return solutions;
        }

        static List<int> givensInRandomOrder (string sudoku)
        {
            var givensIndices = new List<int>();
            int totalGivensIndex = 0;
            while (true)
            {
                var givenIndex = sudoku.Substring(totalGivensIndex).IndexOfAny(new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                if (givenIndex < 0)
                {
                    break;
                }
                givensIndices.Add(totalGivensIndex + givenIndex);
                totalGivensIndex += givenIndex + 1;
            }
            Random rng = new Random(25);
            return givensIndices.OrderBy(a => rng.Next()).ToList();
        }

        static void Main(string[] args)
        {
            var sourceFile = "";
            if (args.Length <= 0 || args[0].Length <= 0)
            {
                sourceFile = @"minimal_sudokus.csv";
            }
            int startFromSudokuIndex = 0;
            try
            {
                startFromSudokuIndex = Int32.Parse(args[1]);
            }
            catch (Exception e) { }

            var csv = loadFile(sourceFile);

            var outputPath = @"improper_sudokus.csv";

            int puzzleCounter = 0;
            foreach (var sudoku in csv)
            {
                while (startFromSudokuIndex-- > 0)
                {
                    continue;
                }
                puzzleCounter++;

                // find improper sudoku with 16 or fewer solutions
                // index givens
                var givensIndices = givensInRandomOrder(sudoku);

                foreach (var givenIndex in givensIndices)
                {
                    // remove given
                    StringBuilder sb = new StringBuilder(sudoku);
                    sb[givenIndex] = '0';
                    var improperSudoku = sb.ToString();
                 
                    // write sudoku to file for conversion to CNF
                    File.WriteAllText(@"WIP_sudoku.txt", improperSudoku);

                    // convert to CNF with python
                    python("sudoku-to-cnf.py", "-i WIP_sudoku.txt -o WIP_sudoku.cnf");

                    // find all solutions
                    var solutions = findSolutions(16);
                    if (solutions.Count > 0)
                    {
                        Console.WriteLine("{0}: sudoku #{1} has {2} solutions.", appTime(), puzzleCounter, solutions.Count);
                        var isWrittenInOutputFile = false;
                        while (!isWrittenInOutputFile)
                        {
                            try
                            {
                                using (StreamWriter sw = File.AppendText(outputPath))
                                {
                                    var newLine = string.Format("{0},{1}", improperSudoku, String.Join("|", solutions));
                                    sw.WriteLine(newLine);
                                    isWrittenInOutputFile = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("{0}: derp ({1})", appTime(), e.Message);
                                System.Threading.Thread.Sleep(100);
                            }
                        }
                        break;
                    }
                    else if (givenIndex == givensIndices.Last())
                    {
                        Console.WriteLine("{0}: sudoku #{1} has no solution set within range.", appTime(), puzzleCounter);
                    }
                }

                //if (puzzleCounter >= 10) break;
            }
            Console.ReadLine();
        }
    }
}
