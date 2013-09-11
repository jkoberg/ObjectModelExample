module ProgramAsync

open System
open System.Net.Http
open VersionOne.SDK.ObjectModel
open OAuth2Client
open FSharp.Data.Json
open FSharp.Data.Json.Extensions


type Threading.Tasks.Task<'T> with
  member this.A = Async.AwaitTask this

let APISCOPE="query-api-1.0"

let QUERY="""
  from: Story
  select:
    - Name
    - Scope.Name
    - Estimate
  """

let definitelyFloat = function
  | JsonValue.Null -> 0.0
  | _ as jval -> jval.AsFloat()

let parse jtxt =
  match JsonValue.Parse(jtxt) with
  | JsonValue.Array [| JsonValue.Array results |] -> 
    [ for i in results ->
       ( i?Name.AsString(), 
         i.["Scope.Name"].AsString(),
         i?Estimate |> definitelyFloat 
       )
    ]
  | _ -> failwith "Didn't receive expected JSON structure" 


let askForAuthCodeAsync (client:AuthClient) = async {
  let url = client.getUrlForGrantRequest()
  Console.SetIn(new IO.StreamReader(Console.OpenStandardInput(8192)));
  printfn "Please visit this url to authorize the permissions:\n\n%s" (url)
  printfn "\nPaste the code here:"
  return! Console.In.ReadLineAsync().A
  }
  
let checkStorageAsync (storage:OAuth2Client.IStorageAsync) = async {
    let! secrets = storage.GetSecretsAsync().A
    let client = AuthClient(secrets, APISCOPE, null, null)
    let! creds = async {
      try
        return! storage.GetCredentialsAsync().A
      with
        :? Exception as ex ->
          let! code = askForAuthCodeAsync client
          return! client.exchangeAuthCodeAsync(code).A
      }
    let! storedcreds = storage.StoreCredentialsAsync(creds).A
    printfn "Secrets and Creds look OK"
    return storage
    }
  
let fetchStoriesStructuredAsync instanceUrl = async {
  let queryUrl = instanceUrl + "/query.v1"
  let! oauth2Storage = checkStorageAsync Storage.JsonFileStorage.Default
  let httpclient = HttpClient.WithOAuth2("query-api-1.0", oauth2Storage)
  let! response = httpclient.PostAsync(queryUrl, new StringContent(QUERY)).A
  let! jtxt = response.Content.ReadAsStringAsync().A
  let results = parse jtxt
  for (name, projName, estimate) in results do
    printfn "%s\t%s\t%f" name projName estimate
  }

let printUsage () = 
  printfn """
      Usage:
     
        example.exe [http://localhost/VersionOne.Web]"

      """
      
open Nito.AsyncEx.Synchronous
let sync a = Async.StartAsTask(a).WaitAndUnwrapException();

let main = function
  | [| |]             -> sync <| fetchStoriesStructuredAsync "http://localhost/VersionOne.Web"; 0
  | [| instanceUrl |] -> sync <| fetchStoriesStructuredAsync instanceUrl; 0
  | _                 -> printUsage(); 1
