module ProgramAsync

open System
open System.Net.Http
open VersionOne.SDK.ObjectModel
open OAuth2Client
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
open Nito.AsyncEx.Synchronous

let sync a = Async.StartAsTask(a).WaitAndUnwrapException();

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

let parse json =
  match JsonValue.Parse(json) with
  | JsonValue.Array [| JsonValue.Array results |] ->
    [ for i in results -> ( i?Name.AsString(),
                            i.["Scope.Name"].AsString(),
                            i?Estimate |> definitelyFloat ) ]
  | _ -> invalidArg "json" "Didn't receive expected json structure"


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
  
exception InvalidResponse of HttpResponseMessage

let query instanceUrl body = async {
  let queryUrl = instanceUrl + "/query.v1"
  let! oauth2Storage = checkStorageAsync Storage.JsonFileStorage.Default
  let httpclient = HttpClient.WithOAuth2("query-api-1.0", oauth2Storage)
  let! response = httpclient.PostAsync(queryUrl, new StringContent(body)).A
  if response.StatusCode = Net.HttpStatusCode.OK then  
    return! response.Content.ReadAsStringAsync().A
  else
    let v = raise (InvalidResponse response)
    return v
  }

let fetchStoriesStructuredAsync instanceUrl = async {
  let! jsonResult = query instanceUrl QUERY
  for name, projName, estimate in parse jsonResult do
    printfn "%s\t%s\t%f" name projName estimate
  }

let printUsage () = 
  printfn """
      Usage:
     
        example.exe [http://localhost/VersionOne.Web]"

      """
      

let main = function
  | [|             |] -> sync <| fetchStoriesStructuredAsync "http://localhost/VersionOne.Web"; 0
  | [| instanceUrl |] -> sync <| fetchStoriesStructuredAsync instanceUrl; 0
  | _                 -> printUsage(); 1
