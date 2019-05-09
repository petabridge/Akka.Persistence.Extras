#### 0.4.1 May 09 2019 ####
* Upgraded to Akka.NET v1.3.13.
* Added the `ConfirmableMessage<T>` type to make it easier to work with `Receive<T>` and `Command<T>` handlers.
* [added clearer warning message when attempting to confirm message that doesn't implement IConfirmableMessage interface](https://github.com/petabridge/Akka.Persistence.Extras/pull/39)
