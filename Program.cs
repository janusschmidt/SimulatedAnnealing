using System.Collections.Immutable;
using System.Diagnostics;

namespace SimulatedAnnealing
{
  record struct Score(int ColPenalty, int RowPenalty, int DiagDownPenalty, int DiagUpPenalty, int Total);
  record struct Result(ImmutableArray<ImmutableArray<int>> Board, Score Score, int NumberOfChanges);

  static class Program
  {
    const int magicalSum = 65;
    const int rowColWeight = 1;
    const int diagWeight = 1;
    const int maxIterations = 2_000;
    const double expFactor = 10;
    const int numberOfExperiments = 4_000;

    static readonly ImmutableArray<ImmutableArray<int>> preDefinedColumns = ImmutableArray.Create(
      ImmutableArray.Create(0, 16, 0, 0, 0),
      ImmutableArray.Create(0, 0, 5, 21, 0),
      ImmutableArray.Create(1, 0, 18, 0, 0),
      ImmutableArray.Create(0, 0, 0, 0, 0),
      ImmutableArray.Create(7, 3, 0, 0, 11));

    static readonly Random rnd = new();

    static readonly ImmutableArray<int> nonPredefinedValues =
      Enumerable.Range(1, 25)
        .Except(preDefinedColumns.SelectMany(x=>x))
        .ToImmutableArray();

    static readonly ImmutableArray<int> numberOfOptionalValuesPerColumn =
      preDefinedColumns
        .Select(col => 5 - col.Count(c => c > 0))
        .ToImmutableArray();

    static void Main()
    {
      Console.WriteLine("Started...");

      var sw = new Stopwatch();
      sw.Start();
      var result = Enumerable.Range(1, numberOfExperiments)
        .AsParallel()
        .Select(_ => CreateRandomBoard())
        .Select(Anneal)
        .OrderBy(x => x.Score.Total).ToArray();

      var perfectResults = result.Count(x => x.Score.Total == 0);

      Console.WriteLine($"Number of perfect results: {perfectResults} ({perfectResults * 100m/numberOfExperiments}%)");

      Console.WriteLine("Best result:");
      PrintResult(result.First());

      Console.WriteLine($"Time taken: {sw.Elapsed}");
    }

    static Result Anneal(ImmutableArray<ImmutableArray<int>> board)
    {
      var bestBoard = board;
      var bestscore = CalculateScore(board);
      var numberOfChanges = 0;
      for (var i = 0; i < maxIterations; i++)
      {
        var newBoard = Swap2AllowedFields(bestBoard);
        var newScore = CalculateScore(newBoard);
        if (newScore.Total < bestscore.Total)
        {
          bestscore = newScore;
          bestBoard = newBoard;
          numberOfChanges++;
        }
        else
        {
          var r = rnd.NextDouble();
          var comp = Math.Exp(-i * (expFactor / maxIterations));
          if (r < comp)
          {
            bestscore = newScore;
            bestBoard = newBoard;
            numberOfChanges++;
          }
        }
      }
      return new Result(bestBoard, bestscore, numberOfChanges);
    }

    static ImmutableArray<ImmutableArray<int>> Swap2AllowedFields(ImmutableArray<ImmutableArray<int>> board)
    {
      var col1 = rnd.Next(5);
      var row1 = RandomAllowedRowNumber(col1);
      var col2 = rnd.Next(5);
      var row2 = RandomAllowedRowNumber(col2);

      var newboard = Swap(board, col1, row1, col2, row2);
      var duplicates = newboard.SelectMany(x => x).GroupBy(x => x);
      if (duplicates.Any(g => g.Count() > 1))
      {
        throw new Exception("Hovsa");
      }
      return newboard;
    }

    static ImmutableArray<ImmutableArray<int>> Swap(ImmutableArray<ImmutableArray<int>> board, int col1, int row1, int col2, int row2)
    {
      var newboard = board.SetItem(col1, board[col1].SetItem(row1, board[col2][row2]));
      return newboard.SetItem(col2, newboard[col2].SetItem(row2, board[col1][row1]));
    }

    static int RandomAllowedRowNumber(int column)
    {
      var numberOfOptionalValues = numberOfOptionalValuesPerColumn[column];
      var optionalValNumber = rnd.Next(numberOfOptionalValues);

      for (var index = 0; index < preDefinedColumns[column].Length; index++)
      {
        var cell = preDefinedColumns[column][index];
        if (cell > 0)
          optionalValNumber++;

        if (index == optionalValNumber)
          return optionalValNumber;
      }

      return optionalValNumber;
    }

    static Score CalculateScore(ImmutableArray<ImmutableArray<int>> board)
    {
      var colPenalty = board.Sum(c => Math.Abs(c.Sum() - magicalSum));
      var rowPenalty = Enumerable.Range(0, 5).Sum(r => Math.Abs(board.Sum(c => c[r]) - magicalSum));
      var diagDownPenalty = Math.Abs(Enumerable.Range(0, 5).Sum(i => board[i][i]) - magicalSum);
      var diagUpPenalty = Math.Abs(Enumerable.Range(0, 5).Sum(i => board[4-i][i]) - magicalSum);
      return new Score(
        colPenalty * rowColWeight,
        rowPenalty * rowColWeight,
        diagDownPenalty * diagWeight,
        diagUpPenalty * diagWeight,
        colPenalty * rowColWeight + rowPenalty * rowColWeight + diagDownPenalty * diagWeight + diagUpPenalty * diagWeight);
    }

    static void PrintResult(params Result[] results)
    {
      foreach (var result in results)
      {
        var (bestBoard, bestscore, numberOfChanges) = result;
        Console.WriteLine($"Solution with score: {bestscore.Total}");
        Console.WriteLine($"Solution with column penalty: {bestscore.ColPenalty}");
        Console.WriteLine($"Solution with row penalty: {bestscore.RowPenalty}");
        Console.WriteLine($"Solution with diagonal up penalty: {bestscore.DiagUpPenalty}");
        Console.WriteLine($"Solution with diagonal down penalty: {bestscore.DiagDownPenalty}");
        Console.WriteLine($"Number of accepted changes: {numberOfChanges}");

        foreach (var ser in bestBoard)
        {
          Console.WriteLine($"{string.Join(",", ser)}");
        }

        Console.WriteLine("********************************************");
      }
    }

    static ImmutableArray<ImmutableArray<int>> CreateRandomBoard()
    {
      var randomNumbers = new Stack<int>(Shuffle(nonPredefinedValues));
      return preDefinedColumns
        .Select(col => col.Select(cell => cell == 0 ? randomNumbers.Pop() : cell).ToImmutableArray())
        .ToImmutableArray();
    }

    static ImmutableArray<int> Shuffle(ImmutableArray<int> original)
    {
      var list = original.ToArray();
      var n = list.Length;
      while (n > 1) {
        n--;
        var k = rnd.Next(n + 1);
        (list[k], list[n]) = (list[n], list[k]);
      }
      return list.ToImmutableArray();
    }
  }
}


