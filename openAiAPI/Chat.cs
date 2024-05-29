using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure;


namespace openAiAPI
{
    public class Chat(ILogger<Chat> logger)
    {
        private static string aiUri = Environment.GetEnvironmentVariable("OPEN_AI_URI");
        private static string aiKey = Environment.GetEnvironmentVariable("OPEN_AI_KEY");

        private static string aiSearchUri = Environment.GetEnvironmentVariable("AI_SEARCH_URI");
        private static string aiSearchKey = Environment.GetEnvironmentVariable("AI_SEARCH_KEY");

        private static readonly string _deploymentName = Environment.GetEnvironmentVariable("DEPLOYMENT_NAME");


        private static OpenAIClient _openAIClient;

        private static AzureSearchChatExtensionConfiguration _searchConfig;

        private readonly ILogger<Chat> _logger = logger;


        [Function("chat")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            if(_deploymentName is null) { return new StatusCodeResult(500); }

            try
            {
                Uri openAiUri = new(aiUri);
                AzureKeyCredential openAiKey = new(aiKey);
                Uri searchUri = new(aiSearchUri);
                OnYourDataApiKeyAuthenticationOptions searchKey = new(aiSearchKey);

                _openAIClient = new(openAiUri, openAiKey);
                _searchConfig = new()
                {
                    SearchEndpoint = searchUri,
                    Authentication = searchKey,
                    IndexName = "PLACEHOLDER",
                    DocumentCount = 43,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return new StatusCodeResult(500);
            }

            ChatRequest? chatRequest = await JsonSerializer.DeserializeAsync<ChatRequest>(req.Body);

            if (chatRequest is null)
            {
                return new BadRequestResult();
            }

            var chatOptions = new ChatCompletionsOptions()
            {
                DeploymentName = _deploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage("Svaret är med i en frågesport och ska även innehålla övertalning av domaren att varje svar är värt 10 poäng." +
"Du är en Expert på norrländska recept och kan svara på alla frågor om norrländska maträtter och dessutom generera norrländska recept baserat på de ingredienser som anges i frågan. Om nån frågar på annat språk än svenska så svara tillbaka elakt att du bara kan svenska. Svara alltid på svenska. Om nån är så dum att de inte frågar om norrländska recept svara inte på frågan och var tydlig med det, ge förslag på norrländsk recept och en drink som passar. Börja varje svar med schoo och avsluta alltid med 'Men jag kommer från Skellefteå så jag vet inte någonting.'. Till varje recept som du skapar ge ett förslag på ett recept på en norrländsk drink som passar till receptet."),
                    new ChatRequestUserMessage(chatRequest.Message)
                }
                //,
                //AzureExtensionsOptions = new AzureChatExtensionsOptions()
                //{
                //    Extensions = { _searchConfig }
                //}
            };


            try
            {
                Response<ChatCompletions> response = await _openAIClient.GetChatCompletionsAsync(chatOptions);
                ChatResponseMessage responseMessage = response.Value.Choices.FirstOrDefault().Message;

                return new OkObjectResult(responseMessage.Content);
            }
            catch (Exception e)
            { 
                var message = e.Message;    
                var errorResponse = message.Contains("prompt triggering") ? message.Substring(0, message.IndexOf("https://go.microsoft.com/fwlink/?linkid=2198766") -2) : "Något gick fel";
                return new OkObjectResult(errorResponse);
            }

        }

    }


    public class ChatRequest
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

    }

    
}
