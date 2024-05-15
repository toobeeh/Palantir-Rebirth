using System.Text.RegularExpressions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Metadata;
using tobeh.Palantir.Commands.XmlDoc;

namespace tobeh.Palantir.Commands.Extensions;

public record CommandFinderRecord(string Command, Command CommandObject, string Description);

public static class CommandFinderExtension
{
    public static List<CommandFinderRecord> FindSimilarCommands(this CommandsExtension extension, string command)
    {
        var commands = ResolveSubcommands(
            ResolveAliases(extension.Commands.Values.Select(c => new CommandFinderRecord(c.Name, c,
                HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(c,
                    "No description provided."))).ToList()));

        var scores = commands
            .Select(name => new { Command = name, Score = ComputeSimilarity(command, name.Command) })
            .OrderByDescending(item => item.Score)
            .Select(item => item.Command)
            .ToList();

        return scores;
    }

    private static List<CommandFinderRecord> ResolveAliases(List<CommandFinderRecord> records)
    {
        return records.SelectMany(c =>
        {
            var aliases = c.CommandObject.Attributes
                .SelectMany(attr => attr is TextAliasAttribute tAttr ? tAttr.Aliases : [])
                .ToList();

            if (aliases.Count == 0)
            {
                return new List<CommandFinderRecord>
                {
                    new(
                        c.Command,
                        c.CommandObject,
                        HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(
                            c.CommandObject, "No description provided.")
                    )
                };
            }

            return aliases.Select(alias => new CommandFinderRecord(alias, c.CommandObject,
                    HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(c.CommandObject,
                        "No description provided.")))
                .Append(new CommandFinderRecord(c.Command, c.CommandObject,
                    HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(c.CommandObject,
                        "No description provided.")));
        }).ToList();
    }

    private static List<CommandFinderRecord> ResolveSubcommands(List<CommandFinderRecord> records)
    {
        return records.SelectMany(c =>
        {
            var subcommands = c.CommandObject.Subcommands
                .Select(sub => new CommandFinderRecord(sub.Name, sub,
                    HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(c.CommandObject,
                        "No description provided.")))
                .ToList();
            if (subcommands.Count == 0)
            {
                return new List<CommandFinderRecord>
                {
                    new(c.Command, c.CommandObject,
                        HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(
                            c.CommandObject, "No description provided."))
                };
            }

            var subAliases = ResolveAliases(subcommands);
            var subSub = ResolveSubcommands(subAliases);
            var appendedSub = subSub
                .Select(sub => new CommandFinderRecord($"{c.Command} {sub.Command}", sub.CommandObject,
                    HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(
                        sub.CommandObject, "No description provided.")))
                .ToList();

            return appendedSub
                .Append(new CommandFinderRecord(c.Command, c.CommandObject,
                    HelpCommandDocumentationMapperEventHandlers.CommandDocumentation.GetValueOrDefault(c.CommandObject,
                        "No description provided.")));
        }).ToList();
    }

    public static double ComputeSimilarity(string str1, string str2)
    {
        var pairs1 = WordLetterPairs(str1.ToUpper());
        var pairs2 = WordLetterPairs(str2.ToUpper());

        int intersection = 0;
        int union = pairs1.Count + pairs2.Count;

        for (int i = 0; i < pairs1.Count; i++)
        {
            for (int j = 0; j < pairs2.Count; j++)
            {
                if (pairs1[i] == pairs2[j])
                {
                    intersection++;
                    pairs2.RemoveAt(
                        j); //Must remove the match to prevent "AAAA" from appearing to match "AA" with 100% success
                    break;
                }
            }
        }

        return (2.0 * intersection * 100) / union; //returns in percentage
        //return (2.0 * intersection) / union; //returns in score from 0 to 1
    }

    // Gets all letter pairs for each
    private static List<string> WordLetterPairs(string str)
    {
        var AllPairs = new List<string>();

        // Tokenize the string and put the tokens/words into an array
        string[] Words = Regex.Split(str, @"\s");

        // For each word
        for (int w = 0; w < Words.Length; w++)
        {
            if (!string.IsNullOrEmpty(Words[w]))
            {
                // Find the pairs of characters
                String[] PairsInWord = LetterPairs(Words[w]);

                for (int p = 0; p < PairsInWord.Length; p++)
                {
                    AllPairs.Add(PairsInWord[p]);
                }
            }
        }

        return AllPairs;
    }

    // Generates an array containing every two consecutive letters in the input string
    private static string[] LetterPairs(string str)
    {
        int numPairs = str.Length - 1;
        string[] pairs = new string[numPairs];

        for (int i = 0; i < numPairs; i++)
        {
            pairs[i] = str.Substring(i, 2);
        }

        return pairs;
    }
}