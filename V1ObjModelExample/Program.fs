open System
open VersionOne.SDK.ObjectModel
open OAuth2Client


let APISCOPE="apiv1"

let askForAuthCode (client:AuthClient) = 
  let url = client.getUrlForGrantRequest()
  System.Console.SetIn(new IO.StreamReader(Console.OpenStandardInput(8192)));
  printfn "Please visit this url to authorize the permissions:\n\n%s" (url)
  printfn "\nPaste the code here:"
  Console.ReadLine()


let checkStorage (storage:OAuth2Client.IStorage) = 
    let secrets = storage.GetSecrets()
    let creds =
      try
        storage.GetCredentials()
      with
        :? IO.FileNotFoundException as ex -> 
          let client = AuthClient(secrets, APISCOPE, null, null)
          let code = askForAuthCode client
          let creds = client.exchangeAuthCode code
          storage.StoreCredentials(creds)
    printfn "Secrets and Creds look OK"
    storage

let fetchStories instanceUrl =
  let oauth2Storage = checkStorage Storage.JsonFileStorage.Default
  let instance = V1Instance(instanceUrl, oauth2Storage)
  let allStories = Filters.StoryFilter()
  let results = query {
    for story in instance.Get.Stories(allStories) do
    let project = instance.Get.ProjectByID(story.Project.ID)
    select (project, story)
    }
  for project, story in results do
    printfn "%s\t%s\t%f" project.Name story.Name (story.Estimate.GetValueOrDefault(0.0))
  
let printUsage () = 
  printfn """
      Usage:
     
        example.exe [http://localhost/VersionOne.Web]"

      """

[<EntryPoint>]
let main argv =
  match argv with 
  | [| |]             -> fetchStories "http://localhost/VersionOne.Web"; 0
  | [| instanceUrl |] -> fetchStories instanceUrl; 0
  | _                 -> printUsage(); 1
