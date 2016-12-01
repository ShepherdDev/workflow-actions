## Lava From Entity

This action takes an attribute that contains a GUID and loads the
entity from the database and then allows you to format that Entity
via Lava. You configure the Entity Type that is expected in the
Settings for the action. Think of this as the `Attribute Set from
Entity` action, except that you can tell it what Entity you want.

##### Configuration

* Entity Type - The `IEntity` type to load.
* Entity Attribute - The `Attribute` that holds the GUID of the
entity object to load.
* Attribute - The `Attribute` to store the results into.
* Lava - The lava text to parse.

##### Example Usage

Let's say we have an Attribute called *Member* that contains a GUID value for a
GroupMember entity and we want to generate some text that says
`Thank you Daniel for joining the Cat Lovers group.`. We
would set the `Entity Type` to GroupMember; `Entity Attribute`
to *Member*; `Attribute` to *Value* and then set our `Lava` to
`Thank you {{ Entity.Person.FirstName }} for joining the {{ 
Entity.Group.Name }} group.`

## Loop Over String

Think of this as a `for` loop in C or C#. This action takes a
string value from an Attribute and splits it into components. Each
component is then used to execute another activity in the workflow.

##### Configuration

* Source - The `Attribute` that has the string.
* Seperator - The string to use when splitting the Source string
(usually a comma).
* Activity - The `Activity` to activate for each iteration.
* Index Key - The key name of the `Attribute` that holds the
current index in the loop in the spawned Activity. (optional)
* Value Key - The key name of the `Attribute` that the element
will be placed into in the spawned Activity. (optional)
* Wait For Completion - Pauses execution of this activity until
all the spawned activities have completed.

##### Example Usage

When the Action fires it split the `Source` attribute by the
contents of the `Seperator` attribute into a temporary array. For
each component of this array it will execute the Activity defined
by the `Activity` attribute. If the activity has an
attributed that matches the value of the `Index Key` attribute
then its value will be set to the integer number of the index of
the element in the array. If the activity has an attribute that
matches the value of the `Value Key` attribute then its value
will be set to string value of the component from the array.

Here is a simple example of a workflow with two Activities:

* Workflow `Main`
  * `Setup`
    * Loop Over String; `Activity` = `Process`
  * `Process`
    * Attribute Set to Group Leader
    * Assign Activity from Attribute Value
    * User Entry Form
    * Set Group Attribute

This will dice the string up into group GUIDs and then spin up
a workflow activity of `Process` for each group. We then load
the group leader from that group, assign the activity to them and
send them an e-mail asking them to fill out a form. Once they
complete the form the value is stored as an attribute in the
group.

>Note: Because this is spawning new activities for each loop
>instance; please be mindful of when you use it with the `Wait For
>Completion` setting set to true. This will cause the workflow to
>persist which means all those spawned activities get saved forever
>in your database. Be mindful anytime you use the `Wait` setting or
>any other actions that cause the workflow to persist, such as the
>`User Entry Form`.

#### Wait For Completion

Some information about the `Wait For Completion` setting. The
way this works is by returning a value that tells the workflow that
this action did not fully complete. Other activities in the
workflow will continue but this activity will be suspended until
the entire workflow is next queued to be run. By default this is
every 10 minutes. What this means is that if you have a workflow
with a loop that is set to `Wait For Completion` then there will
be a delay before it finishes, even if it isn't actually waiting
for anything.

This is because the first time the workflow is it will spawn all
the iteration activities and then the main activity will pause.
During this first run all the spawned activities *may* finish. At
a best case scenario they do finish. Then on the second run of
the workflow it will be able to complete the initial `loop` action
and continue executing that activity which would complete the
workflow. In a standard install of workflows being executed every
10 minutes, this means your workflow may take up to 10 minutes
before it has completed.

Generally this is not a problem, but if you are expecting an
immediate result back from the workflow you may run into issues.
executed
## Activate Activity With Attributes

This provides the same functionality as the normal `Activate
Activity` but provides the additional functionality of being
able to map attribute values from the current activity to the one
being activated. Think of this as passing arguments to the
activity.