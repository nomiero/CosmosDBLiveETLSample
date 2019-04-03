-------------------- Work in progress -------------

## Migration Service 

### Usage 
- Copy the contents of [deployment template](MigrationAppResourceGroup/azuredeploy.json)
- From the portal choose create a resource and create template deployment 

![template deployment](./images/create_template.png)

- Choose build your own template

 ![template](./images/buildtemplate.png)

- Edit the paramers as shown below
    - Choose the resource group location the same as the cosmos db source and destination 
    - take a note of the hostname bindings for the migrationwebappclient name which will be used as the client URI used for migration
    - the cosmos db account information provided is to store the migration metadata and migration state

![template](./images/parameters.png)

- After the template is deployed, open the URL for the migration client that you took note of above
- Add the migration details needed ( the document age parameter is optional, it will start migrating from the beginning if not provided )

![parameters](./images/client.png)

- Once you start the migration, it will take you to the migration status page 

![info](./images/info_page.png)

- You can scale the number of workers up or down by scaling the migrationwebapp service 
- If you want to check more migration metrics over time, you can check the custom metrics published to the app insights instance created with by the ARM template
- Once the migration is complete, you will need to press complete migration to stop streaming from source to destination

### Features
-   Migrations are live as it streams from the source collection to destination.
-	Migrations can be paused and resumed 
-	Failed migration can be resumed
-	Reading from source can be starting from a certain date and that can be used to rollback
-	The tool can be scaled up and down manually during migration to make the best use of storage
-	Uses change feed processor to read from the source collection 
-	Uses bulk executor to write to the target collection 
 

### Components

![Image of migration service](./images/migration_service.png)
-   A continous web job that run in all instances in a web app and this is responsible for reading the change feed using change feed processor and writing to destination using bulk executor 
-   A single instance web job that is responsible for monitoring the migration 
-   A web nodejs app for the frontend that takes inputs to start the migration 
-   The comunication between the web app and the web jobs happens through a cosmosdb database and webjobs poll for any changes in the db to start the migration 

To try it use https://migrationwebappclient.azurewebsites.net
