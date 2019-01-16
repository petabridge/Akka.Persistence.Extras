# Akka.AtLeastOnceDeliveryJournaling

The [`AtLeastOnceDeliveryActor` base type](https://getakka.net/api/Akka.Persistence.AtLeastOnceDeliveryActor.html) in its current form is a total non-pleasure to use in production, for the following reasons:

1. Messages that are queued for reliable delivery via the `Deliver` method aren't automatically persisted, therefore the actor can't fulfill its delviery guarantees unless the user manually writes code for saving the `AtLeastOnceDeliverySnapshot` objects;
2. It's not clear when or how often an `AtLeastOnceDeliverySnapshot` should be saved - there are no clear guidelines for this and actors with a large number of outstanding messages for delivery can pay massive performance penalties, having to save their entire delivery state at once all the time for each incremental change;
3. Saving the `AtLeastOnceDeliverySnapshot` object requires either continuously rewriting one snapshot object without updating its sequence number (bad practice) or some awkard combination with using the normal `Persist` methods BUT without those `Persist` methods doing anything that can interfere with the contiguous state of the delivery snapshot - TL;DR; `Persist` can only really be used for things not directly related to delivery state;
4. In general, this class requires the end user to memorize a significant amount of boilerplate when it comes to actually using it correctly.

The goal of this project is to rewrite the `AtLeastOnceDeliveryActor` to do the following:

1. Automatically persist all messages queued for delivery via the `akka.persistence.journal` - all delivery state is saved atomically at the time it is saved, rather than through continuously overwriting snapshots.
2. Periodically take snapshots and cleanup the journal in the background, as delivery state is churned. Just do this automatically.
3. Automatically recover all delivery state at startup.
4. Don't intend for the user to `Persist` or `Snapshot` anything else inside this actor. Delivery state only. 

We believe this will make the `AtLeastOnceDeliveryActor` more performant (smaller serialization / write-size impact), robust (persisted at all checkpoints), and easier to use ("it just works".)

## Building this solution
To run the build script associated with this solution, execute the following:

**Windows**
```
c:\> build.cmd all
```

**Linux / OS X**
```
c:\> build.sh all
```

If you need any information on the supported commands, please execute the `build.[cmd|sh] help` command.

This build script is powered by [FAKE](https://fake.build/); please see their API documentation should you need to make any changes to the [`build.fsx`](build.fsx) file.

### Conventions
The attached build script will automatically do the following based on the conventions of the project names added to this project:

* Any project name ending with `.Tests` will automatically be treated as a [XUnit2](https://xunit.github.io/) project and will be included during the test stages of this build script;
* Any project name ending with `.Tests` will automatically be treated as a [NBench](https://github.com/petabridge/NBench) project and will be included during the test stages of this build script; and
* Any project meeting neither of these conventions will be treated as a NuGet packaging target and its `.nupkg` file will automatically be placed in the `bin\nuget` folder upon running the `build.[cmd|sh] all` command.

### DocFx for Documentation
This solution also supports [DocFx](http://dotnet.github.io/docfx/) for generating both API documentation and articles to describe the behavior, output, and usages of your project. 

All of the relevant articles you wish to write should be added to the `/docs/articles/` folder and any API documentation you might need will also appear there.

All of the documentation will be statically generated and the output will be placed in the `/docs/_site/` folder. 

#### Previewing Documentation
To preview the documentation for this project, execute the following command at the root of this folder:

```
C:\> serve-docs.cmd
```

This will use the built-in `docfx.console` binary that is installed as part of the NuGet restore process from executing any of the usual `build.cmd` or `build.sh` steps to preview the fully-rendered documentation. For best results, do this immediately after calling `build.cmd buildRelease`.

### Release Notes, Version Numbers, Etc
This project will automatically populate its release notes in all of its modules via the entries written inside [`RELEASE_NOTES.md`](RELEASE_NOTES.md) and will automatically update the versions of all assemblies and NuGet packages via the metadata included inside [`common.props`](src/common.props).

If you add any new projects to the solution created with this template, be sure to add the following line to each one of them in order to ensure that you can take advantage of `common.props` for standardization purposes:

```
<Import Project="..\common.props" />
```

### Code Signing via SignService
This project uses [SignService](https://github.com/onovotny/SignService) to code-sign NuGet packages prior to publication. The `build.cmd` and `build.sh` scripts will automatically download the `SignClient` needed to execute code signing locally on the build agent, but it's still your responsibility to set up the SignService server per the instructions at the linked repository.

Once you've gone through the ropes of setting up a code-signing server, you'll need to set a few configuration options in your project in order to use the `SignClient`:

* Add your Active Directory settings to [`appsettings.json`](appsettings.json) and
* Pass in your signature information to the `signingName`, `signingDescription`, and `signingUrl` values inside `build.fsx`.

Whenever you're ready to run code-signing on the NuGet packages published by `build.fsx`, execute the following command:

```
C:\> build.cmd nuget SignClientSecret={your secret} SignClientUser={your username}
```

This will invoke the `SignClient` and actually execute code signing against your `.nupkg` files prior to NuGet publication.

If one of these two values isn't provided, the code signing stage will skip itself and simply produce unsigned NuGet code packages.