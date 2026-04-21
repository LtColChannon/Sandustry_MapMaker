using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MapMaker
{
    /// <summary>
    /// Main program
    /// </summary>
    internal static class Program
    {
        #region Fields

        // This look up table defines the color for each material in the game (solid materials only and no building blocks)
        internal static readonly Dictionary<Int16, Color> _materialLUT = new Dictionary<Int16, Color>()
        {
            [0] = Color.LightSkyBlue,    // Sky,
            [2] = Color.SandyBrown,      // Sandsoil
            [3] = Color.PaleGreen,       // Spore
            [4] = Color.SaddleBrown,     // Empty Space (under ground)
            [5] = Color.Black,           // Not sure what this is used for
            [6] = Color.DarkSlateBlue,   // Water (under ground)
            [7] = Color.GhostWhite,      // Snow
            [8] = Color.Orange,          // Divider (The game calls this '?')
            [9] = Color.LawnGreen,       // Grass
            [10] = Color.DarkSeaGreen,   // Moss
            [11] = Color.Gold,           // Goldsoil
            [13] = Color.OrangeRed,      // Lava
            [14] = Color.Magenta,        // Fluxite
            [23] = Color.DimGray,        // Bedrock
            [25] = Color.AliceBlue,      // Ice
            [28] = Color.Maroon,         // Redsoil
            [29] = Color.DarkRed,        // Scoria
            [30] = Color.Beige,          // Crackstone
            [103] = Color.CornflowerBlue // Water (above ground)
        };

        // The fallback for unknown elements (Alpha channel will be used to store the original value)
        internal static readonly Color _unknownValueColor = Color.HotPink;

        #endregion

        #region Public Methods

        /// <summary>
        /// Program entry point
        /// </summary>
        /// <param name="args">CLI arguments</param>
        static void Main(String[] args)
        {
            // Just in case
            try
            {
                // That doesn't make any sense
                if (args.Length < 3)
                {
                    // Show the user how to use the program
                    PrintUsage();
                    return;
                }

                // The first argument will be the command (img2json or json2img)
                switch (args[0].ToLower())
                {
                    case "map2img":
                        JsonToImage(args[1], args[2]);
                        break;

                    case "img2map":

                        // Buffer
                        Boolean cleanup = !args.Contains("--no-cleanup");
                        String? template =
                            args.Length > 3 && args[3] != "--no-cleanup"
                            ? args[3]
                            : null;

                        ImageToJson(args[1], args[2], template, cleanup);
                        break;

                    default:
                        PrintUsage();
                        return;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return;
            }

            Console.WriteLine("Done! Happy mining :)");
        }

        /// <summary>
        /// Show the user how to use the program
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("Please use the tool like this:");
            Console.WriteLine("");
            Console.WriteLine("  To create a game file from an image, use:");
            Console.WriteLine("  MapMaker img2map <game-file> <image> [<template_game-file>] [--no-cleanup]");
            Console.WriteLine("  To create an image from a game file (i. e. for debugging), use:");
            Console.WriteLine("  MapMaker map2img <game-file> <image>");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Create an image from a game file
        /// </summary>
        /// <param name="gameFile">The name of the game file</param>
        /// <param name="image">The name of the imge</param>
        private static void JsonToImage(String gameFile, String image)
        {
            // Remove the file types
            gameFile = gameFile.Split('.')[0];
            image = image.Split('.')[0];

            // Get the map from the game file
            Int16[][] matrix = LoadMatrixFromJson(gameFile);

            // Create a new bitmap, which will later contain the map
            Bitmap bitmap = new Bitmap(matrix.Select(r => r.Length).OrderDescending().First(), matrix.Length, PixelFormat.Format32bppArgb);

            // Flag to notify the user about unknown calues
            Boolean unknownValues = false;

            for (Int16 y = 0; y < bitmap.Height; y++)
            {
                for (Int16 x = 0; x < bitmap.Width; x++)
                {
                    // Just in case we have incomplete rows or something like that (Use Sky as material)
                    if (matrix[y].Length < x)
                    {
                        bitmap.SetPixel(y, x, _materialLUT[0]);
                    }
                    else
                    {
                        if (!_materialLUT.ContainsKey(matrix[x][y]))
                        {
                            bitmap.SetPixel(y, x, Color.FromArgb(matrix[x][y], _unknownValueColor));
                            unknownValues = true;
                        }
                        else
                        {
                            bitmap.SetPixel(y, x, _materialLUT[matrix[x][y]]);
                        }
                    }
                }
            }

            bitmap.Save(image + ".png", ImageFormat.Png);

            if (unknownValues)
            {
                Console.WriteLine($"WARNING: Found unknown materials in the map. Search for (semi)-transparent pixels with the color {_unknownValueColor.ToString()}.");
            }
        }

        /// <summary>
        /// Create a game file from an image
        /// </summary>
        /// <param name="gameFile">The name of the game file</param>
        /// <param name="image">The name of the imge</param>
        /// <param name="template">An optional template of the game file.</param>
        /// <param name="cleanup"></param>
        private static void ImageToJson(String gameFile, String image, String? template, Boolean cleanup)
        {
            // Remove the file types
            template = template?.Split('.')[0];
            gameFile = gameFile.Split('.')[0];
            image = image.Split('.')[0];

            // Open the image
            Bitmap bitmap = new Bitmap(image + ".png");

            // Create new map-matrix
            Int16[][] matrix = new Int16[bitmap.Height][];

            for (int y = 0; y < bitmap.Height; y++)
            {
                // Create new row
                matrix[y] = new Int16[bitmap.Width];

                for (int x = 0; x < bitmap.Width; x++)
                {
                    // Get the material number for the map, according to the current pixel value
                    matrix[y][x] = GetMaterialNumber(bitmap.GetPixel(x, y));
                }
            }

            // Buffer
            JsonDocument jsonTemplate;

            // Read the original game file
            String[] gamefileBuffer = File.ReadAllLines(gameFile + ".save");

            if (template != null)
            {
                // Get template game-file
                jsonTemplate = JsonDocument.Parse(File.ReadAllText(template + ".json"));
            }
            else
            {
                // Check length
                if (gamefileBuffer.Length != 2)
                {
                    throw new InvalidDataException($"The game file {gameFile} has an unexpected format.");
                }

                // Use the real game-file instead
                jsonTemplate = JsonDocument.Parse(gamefileBuffer[1]);
            }

            // Get the file header
            String fileHeader = gamefileBuffer[0];

            // Need to convert to a JsonNode
            JsonNode? rootNode = JsonNode.Parse(jsonTemplate.RootElement.ToString());

            // Null check
            if (rootNode == null)
            {
                throw new InvalidOperationException("The given game file (or template file) does not contain any json nodes.");
            }

            // Set map size
            rootNode["world"]?["size"]?["height"] = matrix.Length;
            rootNode["world"]?["size"]?["width"] = matrix[0].Length;

            // Create a json object from the matrix
            JsonArray matrixNode = BuildMatrixJsonArray(matrix);

            // Try to replace the 'matrix' node with our new matrix
            if (!ReplaceNodeRecursive(rootNode, "matrix", matrixNode))
            {
                if (rootNode.GetValueKind() == JsonValueKind.Object)
                {
                    rootNode.AsObject().Add("matrix", matrixNode);
                }

                else if (rootNode.GetValueKind() == JsonValueKind.Array)
                {
                    rootNode.AsArray().Add(matrixNode);
                }
            }

            // Create the horizon array
            JsonArray horizonNode = BuildHorizonJsonArray(matrix, 0, 103, 25);

            // Try to replace the 'horizon' node with our new matrix
            if (!ReplaceNodeRecursive(rootNode, "horizon", horizonNode))
            {
                if (rootNode.GetValueKind() == JsonValueKind.Object)
                {
                    rootNode.AsObject().Add("horizon", horizonNode);
                }

                else if (rootNode.GetValueKind() == JsonValueKind.Array)
                {
                    rootNode.AsArray().Add(horizonNode);
                }
            }

            // Create the ground-horizon array
            JsonArray groundHorizonNode = BuildHorizonJsonArray(matrix, 0);

            // Try to replace the 'groundHorizon' node with our new matrix
            if (!ReplaceNodeRecursive(rootNode, "groundHorizon", groundHorizonNode))
            {
                if (rootNode.GetValueKind() == JsonValueKind.Object)
                {
                    rootNode.AsObject().Add("groundHorizon", groundHorizonNode);
                }

                else if (rootNode.GetValueKind() == JsonValueKind.Array)
                {
                    rootNode.AsArray().Add(groundHorizonNode);
                }
            }

            // Create the chunks array
            JsonArray chunks = BuildChunksJsonArray(matrix[0].Length, matrix.Length);

            // Try to replace the 'groundHorizon' node with our new matrix
            if (!ReplaceNodeRecursive(rootNode, "chunks", chunks))
            {
                if (rootNode.GetValueKind() == JsonValueKind.Object)
                {
                    rootNode.AsObject().Add("chunks", chunks);
                }

                else if (rootNode.GetValueKind() == JsonValueKind.Array)
                {
                    rootNode.AsArray().Add(chunks);
                }
            }

            // Clean unnecessary stuff from the template file
            if (cleanup)
            {
                ReplaceNodeRecursive(rootNode, "fixtures", new JsonArray());
                ReplaceNodeRecursive(rootNode, "structures", new JsonArray());
            }

            // Finally write the game file
            File.WriteAllText(gameFile + ".save", fileHeader);
            File.AppendAllLines(gameFile + ".save", new String[]{ "" });
            File.AppendAllText(gameFile + ".save", rootNode.ToJsonString(new JsonSerializerOptions() { WriteIndented = false }));
        }

        /// <summary>
        /// Builds a new jason array from our map matrix
        /// </summary>
        /// <param name="matrix">The new map matrix</param>
        /// <returns>a new jason array</returns>
        private static JsonArray BuildMatrixJsonArray(Int16[][] matrix)
        {
            // Init ret val
            JsonArray retVal = new JsonArray();

            // Loop over all rows
            for (Int32 y = 0; y < matrix.Length; y++)
            {
                // Init new row
                JsonArray row = new JsonArray();

                // Loop over all cols
                for (Int32 x = 0; x < matrix[y].Length; x++)
                {
                    row.Add(matrix[y][x]);
                }

                retVal.Add(row);
            }

            return retVal;
        }

        /// <summary>
        /// Builds a new 'horizon' (or 'groundHorizon) array from our map matrix
        /// </summary>
        /// <param name="matrix">The new map matrix</param>
        /// <param name="skyMaterials">All material values which count as "Sky"</param>
        /// <returns>a new jason array</returns>
        private static JsonArray BuildHorizonJsonArray(Int16[][] matrix, params Int16[] skyMaterials)
        {
            // Init ret val
            JsonArray retVal = new JsonArray();

            // Get the shortest row
            for (Int32 x = 0; x < matrix.Select(x => x.Length).Order().First(); x++)
            {
                // If the ground goes all the way to the top, set the horizon to null
                Int32 horizon = -1;

                // Search from top to bottom
                for (Int32 y = 0; y < matrix.Length; y++)
                {
                    // Find the first unit which is not allowed
                    if (!skyMaterials.Contains(matrix[y][x]))
                    {
                        break;
                    }
                    else
                    {
                        horizon++;
                    }
                }

                retVal.Add(
                    horizon == -1
                    ? null
                    : horizon);
            }

            // Done
            return retVal;
        }

        /// <summary>
        /// Builds the 'chunks' array (No idea what it does)
        /// </summary>
        /// <returns>the 'chunks' array</returns>
        private static JsonArray BuildChunksJsonArray(Int32 worldWidth, Int32 worldHeight)
        {
            // Init ret val
            JsonArray retVal = new JsonArray();

            // The default array has 32 rows for a world width of 1280 units
            for (Int32 y = 0; y < (worldHeight / 40); y++)
            {
                // Initialize new row
                JsonArray row = new JsonArray();

                // The default array has 32 rows for a world height of 1280 units
                for (Int32 x = 0; x < (worldWidth / 40); x++)
                {
                    row.Add(new JsonObject
                    {
                        ["x"] = x,
                        ["y"] = y,
                        ["shouldUpdate"] = true,
                        ["shouldUpdateNextFrame"] = true
                    });
                }

                retVal.Add(row);
            }

            // Done
            return retVal;
        }

        /// <summary>
        /// Replaces a jason node, which is specified by its name, with a new json node
        /// </summary>
        /// <param name="root">The root which (probybly) contains the node we're looking for</param>
        /// <param name="nodeName">The name of the node we're looking for</param>
        /// <param name="replacementValue">The new value of the node</param>
        /// <returns>true, if the node was replaced</returns>
        private static bool ReplaceNodeRecursive(JsonNode? root, string nodeName, JsonNode replacementValue)
        {
            // We need to differ between objects and arrays
            if (root is JsonObject rootAsObject)
            {
                // Check if the root contains the node we're looking for
                if (rootAsObject.ContainsKey(nodeName))
                {
                    // Replace the node and return
                    rootAsObject[nodeName] = replacementValue;
                    return true;
                }

                // Recursively search all child objects
                foreach (KeyValuePair<String, JsonNode?> childNode in rootAsObject)
                {
                    // Value may be null
                    if (childNode.Value != null)
                    {
                        // Try to replace the node
                        Boolean retVal = ReplaceNodeRecursive(childNode.Value, nodeName, replacementValue);

                        if (retVal)
                        {
                            return true;
                        }
                    }
                }
            }

            // We need to differ between objects and arrays
            else if (root is JsonArray rootAsArray)
            {
                for (Int32 i = 0; i < rootAsArray.Count; i++)
                {
                    // Value may be null
                    if (rootAsArray[i] != null)
                    {
                        // Try to replace the node
                        Boolean retVal = ReplaceNodeRecursive(rootAsArray[i], nodeName, replacementValue);

                        if (retVal)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get the "number" of an in-game material based on the given pixel color
        /// </summary>
        /// <param name="pixel">The pixel to look at</param>
        /// <returns>the "number" of an in-game material</returns>
        private static Int16 GetMaterialNumber(Color pixel)
        {
            // First: Try exact match
            if (_materialLUT.ContainsValue(pixel))
            {
                return _materialLUT.First(x => x.Value == pixel).Key;
            }

            // Next: Try the "unknown value" color
            if (pixel == Color.FromArgb(pixel.A, _unknownValueColor))
            {
                return pixel.A;
            }

            // Finally: Find the best matching color
            Int16 bestIndex = 0;
            Int32 bestDistance = Int32.MaxValue;

            // Try all known colors
            foreach (Int16 index in _materialLUT.Keys)
            {
                // Calculate the 3D-distance from our current pixel color to a known color
                Int32 distR = pixel.R - _materialLUT[index].R;
                Int32 distG = pixel.G - _materialLUT[index].G;
                Int32 distB = pixel.B - _materialLUT[index].B;
                Int32 dist = distR * distR + distG * distG + distB * distB;

                // keep the mallest distance
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = index;
                }
            }

            // Done
            return bestIndex;
        }

        /// <summary>
        /// Loads the matrix (which contains the map) from the given file
        /// </summary>
        /// <param name="gameFile">The name of the game file</param>
        /// <returns>the matrix, which contains the map</returns>
        private static Int16[][] LoadMatrixFromJson(String gameFile)
        {
            // Append file type
            gameFile += ".save";

            // The save file has one json object per line. The second line contains the map
            JsonDocument jsonDoc = JsonDocument.Parse(File.ReadAllLines(gameFile)[1]);

            // Search for the json object called 'matrix'
            if (!TryFindElement(jsonDoc.RootElement, "matrix", out JsonElement matrixElement))
            {
                throw new InvalidOperationException("Unable to find the 'matrix'-element inside the game file.");
            }

            // Check if the 'matrix'-element is valid
            if (matrixElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("The 'matrix'-element is not of type 'Array'.");
            }

            // Buffer for ret val
            List<Int16[]> matrixRows = new List<Int16[]>();

            // Check each row individually
            foreach (JsonElement row in matrixElement.EnumerateArray())
            {
                // Check if the row-element is valid
                if (row.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("The 'matrix'-element contains at least one row, which is not not of type 'Array'.");
                }

                // Check each unit individually
                foreach (JsonElement unit in row.EnumerateArray())
                {
                    // Check if the unit-element is valid
                    if (unit.ValueKind != JsonValueKind.Number)
                    {
                        throw new InvalidOperationException("The 'matrix'-element contains at least one row, which contains at least one unit, which is not not of type 'Number'.");
                    }
                }

                // Parse all the units within the current row and add the row to the ret val
                matrixRows.Add(row.EnumerateArray().Select(u => u.GetInt16()).ToArray());
            }

            // Done
            return matrixRows.ToArray();
        }

        /// <summary>
        /// Tries to find a json element with a given name inside a given root
        /// </summary>
        /// <param name="root">The root which probably contains the element we're looking for</param>
        /// <param name="elementName">The name of the element we're looking for</param>
        /// <param name="result">The element we're looking for</param>
        /// <returns>true, if the element was found</returns>
        private static Boolean TryFindElement(JsonElement root, String elementName, out JsonElement result)
        {
            // We need to differ between objects and arrays
            if (root.ValueKind == JsonValueKind.Object)
            {
                // Check if the root contains the node we're looking for
                if (root.TryGetProperty(elementName, out result))
                {
                    return true;
                }

                // Loop over all objects within root
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    // Recursively search all child objects
                    if (TryFindElement(property.Value, elementName, out result))
                    {
                        return true;
                    }
                }
            }

            // We need to differ between objects and arrays
            else if (root.ValueKind == JsonValueKind.Array)
            {
                // Loop over all elements within root
                foreach (JsonElement subElement in root.EnumerateArray())
                {
                    // Recursively search all child objects
                    if (TryFindElement(subElement, elementName, out result))
                    {
                        return true;
                    }
                }
            }

            // Not found
            result = default;
            return false;
        }

        #endregion
    }
}