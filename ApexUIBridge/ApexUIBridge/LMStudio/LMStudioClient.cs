using System;                       // Basic .NET functionality
using System.Net.Http;              // For making HTTP requests to the AI API
using System.Text;                  // For text encoding
using System.Text.Json;             // For JSON serialization/deserialization
using System.Threading;             // For cancellation support
using System.Threading.Tasks;       // For async programming
using System.Diagnostics;
using System.Text.Json.Serialization;           // For debugging output
using System.IO;                    // For file operations
using System.Collections.Generic;   // For List<T>
using System.Linq;
using System.Text.Json.Nodes;

//var systemMsg = Message.CreateSystemMessage("You are helpful");
//var userMsg = Message.CreateUserTextMessage("Hello!");
//var assistantMsg = Message.CreateAssistantMessage("Hi there!");


namespace LMStudioExampleFormApp
{
    // Main class that handles communication with LM Studio or other compatible AI services
    // Uses constructor with parameters: API endpoint URL, model name, and system prompt
    public class LMStudioExample(string endpoint, string model, string systemPrompt) : IDisposable
    {

        // HttpClient for making API requests - reused across all requests for efficiency
        private readonly HttpClient _httpClient = new HttpClient();
        // Flag to track whether resources have been disposed
        private bool _disposed = false;

        // --- Event declarations for notifying subscribers about request status ---

        // Event triggered when a piece of content is received in streaming mode
        public event EventHandler<string>? OnContentReceived;

        // Event triggered when the entire response is complete
        public event EventHandler<string>? OnComplete;

        // Event triggered when an error occurs
        public event EventHandler<Exception>? OnError;

        // Event triggered when the status of the request changes
        public event EventHandler<string>? OnStatusUpdate;

        private List<Message> messages = new();

 

        public void initialize(long timeoutInSeconds = 100)
        {
            SetTimeout(timeoutInSeconds); // Set timeout here

            messages = new List<Message>()
            {
                Message.CreateSystemMessage(systemPrompt)
            };
        }

        public void SetTimeout(long timeSpanInSeconds)
        {
            var timeout = TimeSpan.FromSeconds(timeSpanInSeconds);
            _httpClient.Timeout = timeout;
        }


        // Method to send a text-only message (maintains backward compatibility)
        public async Task<string> SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            return await SendMessageWithImagesAsync(userMessage, null, cancellationToken);
        }

        // Method to send a message with optional images
        public async Task<string> SendMessageWithImagesAsync(string userMessage, string[]? imagePaths = null, CancellationToken cancellationToken = default)
        {
            // Validate input to avoid sending empty messages
            if (string.IsNullOrEmpty(userMessage))
                throw new ArgumentException("User message cannot be empty", nameof(userMessage));

            // Create user message with text
            var msg = Message.CreateUserTextMessage(userMessage);

            // Add images if provided
            if (imagePaths != null && imagePaths.Length > 0)
            {
                foreach (var imagePath in imagePaths)
                {
                    if (!File.Exists(imagePath))
                    {
                        throw new FileNotFoundException($"Image file not found: {imagePath}");
                    }

                    try
                    {
                        // Read image file and convert to base64
                        var imageBytes = await File.ReadAllBytesAsync(imagePath);
                        var base64String = Convert.ToBase64String(imageBytes);

                        // Determine MIME type based on file extension
                        var mimeType = GetMimeType(imagePath);
                        var dataUrl = $"data:{mimeType};base64,{base64String}";

                        msg.Content.Add(new ImageContent
                        {
                            Type = MessageContentType.ImageUrl,
                            ImageUrl = new ImageUrlData { Url = dataUrl }
                        });
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to process image {imagePath}: {ex.Message}", ex);
                    }
                }
            }

            this.messages.Add(msg);

            try
            {
                // Notify subscribers that we're starting a streaming request
                RaiseStatusUpdate("Sending streaming request...");

                // Build the request content in the format the API expects
                var requestContent = new
                {
                    model = model,                // Model name (e.g., "gemma-3-4b-it")
                    messages = this.messages,
                    temperature = 0.7,            // Controls randomness (0-1)
                    max_tokens = -1,              // Maximum length of response (-1 means no limit)
                    stream = true                 // Enable streaming mode
                };

                // Convert the request object to JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new MessageContentConverter() }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestContent, jsonOptions),  // Convert to JSON string
                    Encoding.UTF8,                             // Use UTF-8 encoding
                    "application/json");                       // Set content type to JSON

                // Create an HTTP request message
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = jsonContent;

                // Send the request with streaming option enabled
                // HttpCompletionOption.ResponseHeadersRead starts processing as soon as headers arrive
                var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,   // Enable streaming
                    cancellationToken);                         // Allow cancellation

                // Check for HTTP error codes
                response.EnsureSuccessStatusCode();
                RaiseStatusUpdate("Processing streaming response...");

                // Process the streaming response
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    string fullResponse = "";  // Accumulate the full response

                    // Continue reading until the stream ends or cancellation is requested
                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        // Read one line at a time
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line))
                            continue;  // Skip empty lines

                        // Server-Sent Events (SSE) format uses "data: " prefix
                        if (line.StartsWith("data: "))
                        {
                            // Extract the JSON data from the line
                            var jsonData = line.Substring(6).Trim();  // Remove "data: " prefix
                            if (jsonData == "[DONE]")
                                break;  // Special marker indicating end of stream

                            try
                            {
                                // Parse the JSON chunk into our StreamingResponse class
                                var chunk = JsonSerializer.Deserialize<StreamingResponse>(jsonData);
                                if (chunk?.choices != null && chunk.choices.Length > 0)
                                {
                                    var choice = chunk.choices[0];
                                    // Check if there's content in this chunk
                                    if (choice.delta != null && !string.IsNullOrEmpty(choice.delta.content))
                                    {
                                        var content = choice.delta.content;
                                        fullResponse += content;  // Add to accumulated response
                                        RaiseContentReceived(content);  // Notify subscribers
                                    }
                                }
                            }
                            catch (JsonException ex)
                            {
                                // Log JSON parsing errors but continue processing
                                Debug.WriteLine($"JSON parsing error: {ex.Message}");
                                Debug.WriteLine($"Problematic JSON: {jsonData}");
                            }
                        }
                    }

                    var msgAssistant = Message.CreateAssistantMessage(fullResponse);
                    this.messages.Add(msgAssistant);

                    // If not cancelled, notify that the response is complete
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        RaiseComplete(fullResponse);
                        return fullResponse;
                    }
                    else
                    {
                        throw new OperationCanceledException("The operation was canceled.");
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                // Handle CanceledException
                RaiseError(ex);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during the process
                RaiseError(ex);
            }
            finally
            {
                // If cancelled, notify that the response was cancelled
                if (cancellationToken.IsCancellationRequested)
                {
                    RaiseComplete($"Error: The operation was canceled.");
                }
            }

            return "";  // Return empty string if no response was received
        }

        // Method for non-streaming API requests with image support
        public async Task<string> SendMessageNonStreamingAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            return await SendMessageWithImagesNonStreamingAsync(userMessage, null, cancellationToken);
        }

        public async Task<string> SendMessageWithImagesNonStreamingAsync(string userMessage, string[]? imagePaths = null, CancellationToken cancellationToken = default)
        {
            // Validate input
            if (string.IsNullOrEmpty(userMessage))
                throw new ArgumentException("User message cannot be empty", nameof(userMessage));

            try
            {
                // Notify subscribers about starting a non-streaming request
                RaiseStatusUpdate("Sending non-streaming request...");

                // Create user message with text
                var msg = Message.CreateUserTextMessage(userMessage);

                // Add images if provided
                if (imagePaths != null && imagePaths.Length > 0)
                {
                    foreach (var imagePath in imagePaths)
                    {
                        if (!File.Exists(imagePath))
                        {
                            throw new FileNotFoundException($"Image file not found: {imagePath}");
                        }

                        try
                        {
                            // Read image file and convert to base64
                            var imageBytes = await File.ReadAllBytesAsync(imagePath);
                            var base64String = Convert.ToBase64String(imageBytes);

                            // Determine MIME type based on file extension
                            var mimeType = GetMimeType(imagePath);
                            var dataUrl = $"data:{mimeType};base64,{base64String}";

                            msg.Content.Add(new ImageContent
                            {
                                Type = MessageContentType.ImageUrl,
                                ImageUrl = new ImageUrlData { Url = dataUrl }
                            });
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Failed to process image {imagePath}: {ex.Message}", ex);
                        }
                    }
                }

                this.messages.Add(msg);

                // Build the request content (similar to streaming, but with stream=false)
                var requestContent = new
                {
                    model = model,
                    messages = this.messages,
                    temperature = 0.7,
                    max_tokens = -1,
                    stream = false  // Disable streaming
                };

                // Convert to JSON and prepare the content
                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new MessageContentConverter() }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestContent, jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                // Send the request and wait for the full response
                var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
              
                
                
                response.EnsureSuccessStatusCode();

                RaiseStatusUpdate("Processing non-streaming response...");

                // Read the complete response JSON
                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Deserialize with custom options
                jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new MessageContentConverter() }
                };

                var aiResponse = JsonSerializer.Deserialize<AIMessage>(jsonResponse, jsonOptions);

                // Extract the message content
                if (aiResponse?.choices != null && aiResponse.choices.Length > 0)
                {
                    var responseContent = aiResponse?.choices[0]?.message?.GetTextContent() ?? "";
                    Debug.WriteLine($"Non-streaming response received: {responseContent.Length} characters");
                    RaiseComplete(responseContent);  // Notify subscribers

                    var msgAssistant = Message.CreateAssistantMessage(responseContent);
                    this.messages.Add(msgAssistant);

                    return responseContent;  // Return the full response
                }
                else
                {
                    // Handle invalid response format
                    throw new Exception("Invalid response format");
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                RaiseError(ex);
                throw;  // Re-throw to allow caller to handle the error
            }
        }























        /// <summary>
        /// Requests text embeddings from the LM Studio API
        /// </summary>
        /// <param name="text">The text to generate embeddings for</param>
        /// <param name="embeddingModel">The embedding model to use (e.g., "text-embedding-nomic-embed-text-v1.5")</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of embedding vectors (float arrays)</returns>
        public async Task<float[]?> GetEmbeddingAsync(
            string text,
            string embeddingModel = "text-embedding-nomic-embed-text-v1.5",
            CancellationToken cancellationToken = default)
        {
            // Validate input
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Text cannot be empty", nameof(text));

            if (string.IsNullOrEmpty(embeddingModel))
                throw new ArgumentException("Embedding model cannot be empty", nameof(embeddingModel));

            try
            {
                RaiseStatusUpdate("Requesting embeddings...");

                // Build the embedding request
                var requestContent = new EmbeddingRequest
                {
                    Model = embeddingModel,
                    Input = text
                };

                // Serialize to JSON
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestContent),
                    Encoding.UTF8,
                    "application/json");

                // Construct the embedding endpoint URL
                // Replace the chat completions endpoint with the embeddings endpoint
                var embeddingEndpoint = endpoint.Replace("/v1/chat/completions", "/api/v0/embeddings");

                Debug.WriteLine($"Sending embedding request to: {embeddingEndpoint}");

                // Send the request
                var response = await _httpClient.PostAsync(
                    embeddingEndpoint,
                    jsonContent,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                RaiseStatusUpdate("Processing embedding response...");

                // Read and parse the response
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(jsonResponse);

                // Extract the embedding vector
                if (embeddingResponse?.Data != null && embeddingResponse.Data.Length > 0)
                {
                    var embedding = embeddingResponse.Data[0].Embedding;
                    Debug.WriteLine($"Embedding received: {embedding?.Length ?? 0} dimensions");
                    RaiseStatusUpdate("Embedding completed");
                    return embedding;
                }
                else
                {
                    throw new Exception("Invalid embedding response format");
                }
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Requests embeddings for multiple texts in a single batch request
        /// </summary>
        /// <param name="texts">Array of texts to generate embeddings for</param>
        /// <param name="embeddingModel">The embedding model to use</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of embedding vectors, one for each input text</returns>
        public async Task<float[][]?> GetEmbeddingsBatchAsync(
            string[] texts,
            string embeddingModel = "text-embedding-nomic-embed-text-v1.5",
            CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Length == 0)
                throw new ArgumentException("Texts array cannot be null or empty", nameof(texts));

            try
            {
                RaiseStatusUpdate($"Requesting embeddings for {texts.Length} texts...");

                var embeddings = new List<float[]>();

                // Process each text individually
                // Note: Some APIs support batch input as an array, but we'll process sequentially for compatibility
                foreach (var text in texts)
                {
                    var embedding = await GetEmbeddingAsync(text, embeddingModel, cancellationToken);
                    if (embedding != null)
                    {
                        embeddings.Add(embedding);
                    }
                }

                Debug.WriteLine($"Batch embeddings completed: {embeddings.Count} embeddings generated");
                RaiseStatusUpdate("Batch embedding completed");

                return embeddings.ToArray();
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }












        /// <summary>
        /// Gets a list of all available models (both loaded and downloaded)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of ModelInfo objects</returns>
        public async Task<ModelInfo[]?> GetAllModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                RaiseStatusUpdate("Fetching models list...");

                // Construct the models endpoint URL
                var modelsEndpoint = endpoint.Replace("/v1/chat/completions", "/api/v0/models");

                Debug.WriteLine($"Fetching models from: {modelsEndpoint}");

                // Send GET request to the models endpoint
                var response = await _httpClient.GetAsync(modelsEndpoint, cancellationToken);
                response.EnsureSuccessStatusCode();

                RaiseStatusUpdate("Processing models list...");

                // Read and parse the response
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var modelsResponse = JsonSerializer.Deserialize<ModelsListResponse>(jsonResponse);

                if (modelsResponse?.Data != null)
                {
                    Debug.WriteLine($"Found {modelsResponse.Data.Length} models");
                    RaiseStatusUpdate($"Found {modelsResponse.Data.Length} models");
                    return modelsResponse.Data;
                }
                else
                {
                    throw new Exception("Invalid models list response format");
                }
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets detailed information about a specific model by its ID
        /// </summary>
        /// <param name="modelId">The model ID (e.g., "qwen2-vl-7b-instruct")</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>ModelInfo object with detailed information</returns>
        public async Task<ModelInfo?> GetModelInfoAsync(string modelId, CancellationToken cancellationToken = default)
        {
            // Validate input
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentException("Model ID cannot be empty", nameof(modelId));

            try
            {
                RaiseStatusUpdate($"Fetching info for model: {modelId}...");

                // Construct the specific model endpoint URL
                var modelEndpoint = endpoint.Replace("/v1/chat/completions", $"/api/v0/models/{modelId}");

                Debug.WriteLine($"Fetching model info from: {modelEndpoint}");

                // Send GET request to the specific model endpoint
                var response = await _httpClient.GetAsync(modelEndpoint, cancellationToken);
                response.EnsureSuccessStatusCode();

                RaiseStatusUpdate("Processing model info...");

                // Read and parse the response
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var modelInfo = JsonSerializer.Deserialize<ModelInfo>(jsonResponse);

                if (modelInfo != null)
                {
                    Debug.WriteLine($"Model info retrieved: {modelInfo}");
                    RaiseStatusUpdate($"Model info retrieved: {modelInfo.Id}");
                    return modelInfo;
                }
                else
                {
                    throw new Exception("Invalid model info response format");
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Handle case where model doesn't exist
                var notFoundEx = new Exception($"Model '{modelId}' not found", ex);
                RaiseError(notFoundEx);
                throw notFoundEx;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all loaded models (models currently in memory and ready to use)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of loaded ModelInfo objects</returns>
        public async Task<ModelInfo[]?> GetLoadedModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allModels = await GetAllModelsAsync(cancellationToken);

                if (allModels == null)
                    return null;

                // Filter for only loaded models
                var loadedModels = allModels.Where(m => m.IsLoaded).ToArray();

                Debug.WriteLine($"Found {loadedModels.Length} loaded models out of {allModels.Length} total");
                RaiseStatusUpdate($"Found {loadedModels.Length} loaded models");

                return loadedModels;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all embedding models (both loaded and not loaded)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of embedding ModelInfo objects</returns>
        public async Task<ModelInfo[]?> GetEmbeddingModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allModels = await GetAllModelsAsync(cancellationToken);

                if (allModels == null)
                    return null;

                // Filter for only embedding models
                var embeddingModels = allModels.Where(m => m.IsEmbeddingModel).ToArray();

                Debug.WriteLine($"Found {embeddingModels.Length} embedding models");
                RaiseStatusUpdate($"Found {embeddingModels.Length} embedding models");

                return embeddingModels;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all language models (LLMs)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of LLM ModelInfo objects</returns>
        public async Task<ModelInfo[]?> GetLanguageModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allModels = await GetAllModelsAsync(cancellationToken);

                if (allModels == null)
                    return null;

                // Filter for only language models
                var llmModels = allModels.Where(m => m.IsLanguageModel).ToArray();

                Debug.WriteLine($"Found {llmModels.Length} language models");
                RaiseStatusUpdate($"Found {llmModels.Length} language models");

                return llmModels;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all vision-language models (VLMs)
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of VLM ModelInfo objects</returns>
        public async Task<ModelInfo[]?> GetVisionModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allModels = await GetAllModelsAsync(cancellationToken);

                if (allModels == null)
                    return null;

                // Filter for only vision models
                var visionModels = allModels.Where(m => m.IsVisionModel).ToArray();

                Debug.WriteLine($"Found {visionModels.Length} vision-language models");
                RaiseStatusUpdate($"Found {visionModels.Length} vision-language models");

                return visionModels;
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                throw;
            }
        }














        /// <summary>
        /// Calculates cosine similarity between two embedding vectors
        /// Useful for comparing semantic similarity between texts
        /// </summary>
        /// <param name="embedding1">First embedding vector</param>
        /// <param name="embedding2">Second embedding vector</param>
        /// <returns>Similarity score between -1 and 1 (1 = identical, 0 = orthogonal, -1 = opposite)</returns>
        public static float CalculateCosineSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1 == null || embedding2 == null)
                throw new ArgumentNullException("Embeddings cannot be null");

            if (embedding1.Length != embedding2.Length)
                throw new ArgumentException("Embeddings must have the same dimensions");

            float dotProduct = 0f;
            float magnitude1 = 0f;
            float magnitude2 = 0f;

            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                magnitude1 += embedding1[i] * embedding1[i];
                magnitude2 += embedding2[i] * embedding2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            if (magnitude1 == 0f || magnitude2 == 0f)
                return 0f;

            return dotProduct / (magnitude1 * magnitude2);
        }

         
        // Helper method to determine MIME type from file extension
        private string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/jpeg" // Default to JPEG
            };
        }

        // --- Helper methods for raising events ---

        // Notify subscribers when content is received (in streaming mode)
        private void RaiseContentReceived(string content)
        {
            Debug.WriteLine($"Content received: {content}");
            OnContentReceived?.Invoke(this, content);  // The '?' ensures we only call if there are subscribers
        }

        // Notify subscribers when the response is complete
        private void RaiseComplete(string fullResponse)
        {
            Debug.WriteLine($"Completed with full response of {fullResponse.Length} characters");
            OnComplete?.Invoke(this, fullResponse);
        }

        // Notify subscribers when an error occurs
        private void RaiseError(Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}");
            OnError?.Invoke(this, ex);
        }

        // Notify subscribers of status updates
        private void RaiseStatusUpdate(string status)
        {
            Debug.WriteLine($"Status: {status}");
            OnStatusUpdate?.Invoke(this, status);
        }

        // --- IDisposable implementation to clean up resources ---

        // Public Dispose method called by clients
        public void Dispose()
        {
            Dispose(true);  // Dispose managed and unmanaged resources
            GC.SuppressFinalize(this);  // Tell GC not to call finalize method
        }

        // Protected method that actually performs the disposal
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)  // Only dispose once
            {
                if (disposing)  // If we're disposing managed resources
                {
                    _httpClient?.Dispose();  // Clean up the HttpClient
                }
                _disposed = true;  // Mark as disposed
            }
        }
    }

    // --- Message Content Type Constants ---
    public static class MessageContentType
    {
        public const string Text = "text";
        public const string ImageUrl = "image_url";
    }

    // --- Interface for message content ---
    public interface IMessageContent
    {
        string? Type { get; set; }
    }

    // --- Text content implementation ---
    public class TextContent : IMessageContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; } = MessageContentType.Text;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    // --- Image content implementation ---
    public class ImageContent : IMessageContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; } = MessageContentType.ImageUrl;

        [JsonPropertyName("image_url")]
        public ImageUrlData? ImageUrl { get; set; }
    }

    // --- Custom JSON converter for polymorphic IMessageContent ---
    public class MessageContentConverter : JsonConverter<IMessageContent>
    {
        public override IMessageContent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // For reading responses, we'll parse as JsonNode first
            var node = JsonNode.Parse(ref reader);
            if (node == null) return null;

            var typeValue = node["type"]?.GetValue<string>();

            return typeValue switch
            {
                MessageContentType.Text => node.Deserialize<TextContent>(options),
                MessageContentType.ImageUrl => node.Deserialize<ImageContent>(options),
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, IMessageContent value, JsonSerializerOptions options)
        {
            // Serialize the concrete type directly
            if (value is TextContent textContent)
            {
                JsonSerializer.Serialize(writer, textContent, options);
            }
            else if (value is ImageContent imageContent)
            {
                JsonSerializer.Serialize(writer, imageContent, options);
            }
        }
    }

    // --- Custom converter to handle both string and List<IMessageContent> for content field ---
    public class FlexibleContentConverter : JsonConverter<List<IMessageContent>>
    {
        public override List<IMessageContent>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Check if it's a string (API response format)
            if (reader.TokenType == JsonTokenType.String)
            {
                var textContent = reader.GetString();
                return new List<IMessageContent>
                {
                    new TextContent
                    {
                        Type = MessageContentType.Text,
                        Text = textContent
                    }
                };
            }
            // Otherwise, it's an array (our request format)
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<IMessageContent>();
                var contentConverter = new MessageContentConverter();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;

                    var item = contentConverter.Read(ref reader, typeof(IMessageContent), options);
                    if (item != null)
                        list.Add(item);
                }

                return list;
            }

            return new List<IMessageContent>();
        }

        public override void Write(Utf8JsonWriter writer, List<IMessageContent> value, JsonSerializerOptions options)
        {
            // Always write as array
            writer.WriteStartArray();
            var contentConverter = new MessageContentConverter();

            foreach (var item in value)
            {
                contentConverter.Write(writer, item, options);
            }

            writer.WriteEndArray();
        }
    }

    // --- Image URL data structure ---
    public class ImageUrlData
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    // --- Message class with factory methods ---
    public class Message
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        [JsonConverter(typeof(FlexibleContentConverter))]
        public List<IMessageContent> Content { get; set; }

        public Message(string role)
        {
            Role = role;
            Content = new List<IMessageContent>();
        }

        // Factory method to create a system message
        public static Message CreateSystemMessage(string text)
        {
            var message = new Message("system");
            message.Content.Add(new TextContent
            {
                Type = MessageContentType.Text,
                Text = text
            });
            return message;
        }

        // Factory method to create a user text message
        public static Message CreateUserTextMessage(string text)
        {
            var message = new Message("user");
            message.Content.Add(new TextContent
            {
                Type = MessageContentType.Text,
                Text = text
            });
            return message;
        }

        // Factory method to create an assistant message
        public static Message CreateAssistantMessage(string text)
        {
            var message = new Message("assistant");
            message.Content.Add(new TextContent
            {
                Type = MessageContentType.Text,
                Text = text
            });
            return message;
        }

        // Helper method to get text content from the message
        public string GetTextContent()
        {
            if (Content == null) return "";

            var textItems = Content.OfType<TextContent>().Where(c => !string.IsNullOrEmpty(c.Text));
            return string.Join(" ", textItems.Select(c => c.Text));
        }
    }

    // --- JSON response models for parsing API responses ---

    // Model for streaming response format
    public class StreamingResponse
    {
        public string? id { get; set; }                // Unique identifier for the response
        public string? @object { get; set; }           // Type of object (usually "chat.completion.chunk")
        public int created { get; set; }               // Timestamp when the response was created
        public string? model { get; set; }             // Model ID that generated the response
        public StreamingChoice[]? choices { get; set; } // Array of content choices (usually just one)
    }

    // Represents one chunk of a streaming response
    public class StreamingChoice
    {
        public int index { get; set; }                 // Index of this choice (usually 0)
        public DeltaMessage? delta { get; set; }       // The new content in this chunk
        public string? finish_reason { get; set; }     // Why the response finished (null, "stop", "length", etc.)
    }

    // Contains the actual content delta in a streaming response
    public class DeltaMessage
    {
        public string? role { get; set; }              // Role (usually "assistant")
        public string? content { get; set; }           // The actual text content
    }

    // --- JSON models for non-streaming responses ---

    // Model for complete, non-streaming API response
    public class AIMessage
    {
        public string? id { get; set; }                // Unique identifier for the response
        public string? @object { get; set; }           // Type of object (usually "chat.completion")
        public int created { get; set; }               // Timestamp when the response was created
        public string? model { get; set; }             // Model ID that generated the response
        public Choice[]? choices { get; set; }         // Array of content choices (usually just one)
        public Usage? usage { get; set; }              // Token usage statistics
    }

    // Represents one complete response option
    public class Choice
    {
        public int index { get; set; }                 // Index of this choice (usually 0)
        public Message? message { get; set; }          // The complete message
        public string? finish_reason { get; set; }     // Why the response finished ("stop", "length", etc.)
    }

    // Token usage information
    public class Usage
    {
        public int prompt_tokens { get; set; }         // Tokens used in the input prompt
        public int completion_tokens { get; set; }     // Tokens used in the generated response
        public int total_tokens { get; set; }          // Total tokens used
    }


     




    // --- Embedding Request Models ---
    public class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("input")]
        public string? Input { get; set; }
    }

    // --- Embedding Response Models ---
    public class EmbeddingResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("data")]
        public EmbeddingData[]? Data { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("usage")]
        public EmbeddingUsage? Usage { get; set; }
    }

    public class EmbeddingData
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }

    public class EmbeddingUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

     




    /// <summary>
    /// Response containing a list of all available models
    /// </summary>
    public class ModelsListResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("data")]
        public ModelInfo[]? Data { get; set; }
    }

    /// <summary>
    /// Detailed information about a specific model
    /// </summary>
    public class ModelInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }

        [JsonPropertyName("arch")]
        public string? Arch { get; set; }

        [JsonPropertyName("compatibility_type")]
        public string? CompatibilityType { get; set; }

        [JsonPropertyName("quantization")]
        public string? Quantization { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("max_context_length")]
        public int MaxContextLength { get; set; }

        /// <summary>
        /// Helper property to check if model is currently loaded
        /// </summary>
        public bool IsLoaded => State?.ToLowerInvariant() == "loaded";

        /// <summary>
        /// Helper property to check if this is an embedding model
        /// </summary>
        public bool IsEmbeddingModel => Type?.ToLowerInvariant() == "embeddings";

        /// <summary>
        /// Helper property to check if this is a language model (LLM)
        /// </summary>
        public bool IsLanguageModel => Type?.ToLowerInvariant() == "llm";

        /// <summary>
        /// Helper property to check if this is a vision-language model (VLM)
        /// </summary>
        public bool IsVisionModel => Type?.ToLowerInvariant() == "vlm";

        /// <summary>
        /// Returns a human-readable description of the model
        /// </summary>
        public override string ToString()
        {
            return $"{Id} ({Type}, {State}, {Quantization})";
        }
    }




}