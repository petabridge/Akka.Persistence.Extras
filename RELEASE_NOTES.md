#### 0.4.0 April 02 2019 ####
* Upgraded to Akka.NET v1.3.12.
* Added the `PersistenceSupervisor` to Akka.Persistence.Extras. This [actor is responsible for providing a more robust Akka.Persistence failure, recovery, and retry supervision model for Akka.NET](https://devops.petabridge.com/articles/state-management/akkadotnet-persistence-failure-handling.html).