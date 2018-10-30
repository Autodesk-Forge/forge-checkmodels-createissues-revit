# data.management-csharp-webhook

![Platforms](https://img.shields.io/badge/platform-Windows|MacOS-lightgray.svg)
![.NET](https://img.shields.io/badge/.NET%20Core-2.1-blue.svg)
[![License](http://img.shields.io/:license-MIT-blue.svg)](http://opensource.org/licenses/MIT)

[![oAuth2](https://img.shields.io/badge/oAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![Data-Management](https://img.shields.io/badge/Data%20Management-v1-green.svg)](http://developer.autodesk.com/)
[![Webhook](https://img.shields.io/badge/Webhook-v1-green.svg)](http://developer.autodesk.com/)


![Advanced](https://img.shields.io/badge/Level-Advanced-red.svg)

# Description

Show BIM 360 Hubs, Projects and Files, based on [this tutorial](http://learnforge.autodesk.io). When select a folder on the tree view, the option to "Start watching folder" allow to create a `dm.version.added` webhook for that folder. When a new file is uploaded (e.g. via BIM 360 Docs UI) the webhook notifies the app, which queues the job to, later when ready, access the metadata of the file.

## Thumbnail

![](thumbnail.png)

## Demonstration

There a few moving parts on this sample, [this video](https://www.youtube.com/watch?v=S35g6ZMHDXs)  demonstrates the sample.

# Setup

## Prerequisites

1. **Forge Account**: Learn how to create a Forge Account, activate subscription and create an app at [this tutorial](http://learnforge.autodesk.io/#/account/). 
2. **Visual Studio**: Either Community (Windows) or Code (Windows, MacOS).
3. **.NET Core** basic knowledge with C#
4. **ngrok**: Routing tool, [download here](https://ngrok.com/)
5. **MongoDB**: noSQL database, [learn more](https://www.mongodb.com/). Or use a online version via [mLab](https://mlab.com/) (this is used on this sample)

## Running locally

Clone this project or download it. It's recommended to install [GitHub desktop](https://desktop.github.com/). To clone it via command line, use the following (**Terminal** on MacOSX/Linux, **Git Shell** on Windows):

    git clone https://github.com/autodesk-forge/data.management-csharp-webhook

**Visual Studio** (Windows):

Right-click on the project, then go to **Debug**. Adjust the settings as shown below. 

![](webhook/wwwroot/img/readme/visual_studio_settings.png) 

**Visual Sutdio Code** (Windows, MacOS):

Open the folder, at the bottom-right, select **Yes** and **Restore**. This restores the packages (e.g. Autodesk.Forge) and creates the launch.json file. See *Tips & Tricks* for .NET Core on MacOS.

![](webhook/wwwroot/img/readme/visual_code_restore.png)

**MongoDB**

[MongoDB](https://www.mongodb.com) is a no-SQL database based on "documents", which stores JSON-like data. For testing purpouses, you can either use local or live. One easy way is using [mLab](https://mlab.com).

1. Create a account
2. Create a new database (e.g. named `webhooks`)
3. Under **Collections**, add a new `users` collection
4. Under **Users**, add a new database user, e.g. `appuser`

At this point the connection string should be in the form of `mongodb://<dbuser>:<dbpassword>@ds<number>.mlab.com:<port>/webhook`. 

There are several tools to view your database, [Robo 3T](https://robomongo.org/) (formerly Robomongo) is a free lightweight GUI that can be used. When it opens, click on **Create**, then at **Connection** specify a `name`, enter your `ds<number>.mlab.com` as address and `<port>` number as port, also at **Authentication** enter the datbase name (e.g. `webhook`) and `<dbuser>` and `<dbpassword>`.

**Environment variables**

At the `.vscode\launch.json`, find the env vars and add your Forge Client ID, Secret and callback URL. Also define the `ASPNETCORE_URLS` variable. The end result should be as shown below:

```json
"env": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "ASPNETCORE_URLS" : "http://localhost:3000",
    "FORGE_CLIENT_ID": "your id here",
    "FORGE_CLIENT_SECRET": "your secret here",
    "FORGE_CALLBACK_URL": "http://localhost:3000/api/forge/callback/oauth",
    "FORGE_WEBHOOK_CALLBACK_URL": "http://1234.ngrok.io/api/forge/callback/webhook",
    "OAUTH_DATABASE": "mongodb://<dbuser>:<dbpassword>@ds<number>.mlab.com:<port>/webhook"
},
```

Open `http://localhost:3000` to start the app.

Open `http://localhost:3000/hangfire` for jobs dashboard.

## Deployment

To deploy this application to Heroku, the **Callback URL** for Forge must use your `.herokuapp.com` address. After clicking on the button below, at the Heroku Create New App page, set your Client ID, Secret and Callback URL for Forge.

[![Deploy](https://www.herokucdn.com/deploy/button.svg)](https://heroku.com/deploy)


# Further Reading

Documentation:

- [BIM 360 API](https://developer.autodesk.com/en/docs/bim360/v1/overview/) and [App Provisioning](https://forge.autodesk.com/blog/bim-360-docs-provisioning-forge-apps)
- [Data Management API](https://developer.autodesk.com/en/docs/data/v2/overview/)
- [Webhook](https://forge.autodesk.com/en/docs/webhooks/v1)

Other APIs:

- [Hangfire](https://www.hangfire.io/) queueing library for .NET
- [MongoDB for C#](https://docs.mongodb.com/ecosystem/drivers/csharp/) driver
- [mLab](https://mlab.com/) Database-as-a-Service for MongoDB

### Known Issues

- **No webhook for translation**: this sample tries `GET Manifest` every interval as, as of now, there is no webhook for Model Derivative translations on BIM 360 files. There is support for OSS Buckets translations, [learn more here](https://forge.autodesk.com/en/docs/webhooks/v1/tutorials/create-a-hook-model-derivative/). 

### Tips & Tricks

This sample uses .NET Core and works fine on both Windows and MacOS, see [this tutorial for MacOS](https://github.com/augustogoncalves/dotnetcoreheroku).

### Troubleshooting

1. **Cannot see my BIM 360 projects**: Make sure to provision the Forge App Client ID within the BIM 360 Account, [learn more here](https://forge.autodesk.com/blog/bim-360-docs-provisioning-forge-apps). This requires the Account Admin permission.

2. **error setting certificate verify locations** error: may happen on Windows, use the following: `git config --global http.sslverify "false"`

## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.

## Written by

Augusto Goncalves [@augustomaia](https://twitter.com/augustomaia), [Forge Partner Development](http://forge.autodesk.com)