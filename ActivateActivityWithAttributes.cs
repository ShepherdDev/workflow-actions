using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Rock.Workflow.Action
{
    /// <summary>
    /// Activates a new activity for a given activity type
    /// </summary>
    [ActionCategory( "Shepherd Church" )]
    [Description( "Activates a new activity instance and all of its actions while passing attribute values." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Activate Activity With Attributes" )]

    [WorkflowActivityType( "Activity", "The activity type to activate", true, "", "", 0 )]
    [KeyValueListField( "Attribute Values", "Used to assign values to the activities attributes. <span class='tip tip-lava'><span>", false, keyPrompt: "Attribute", valuePrompt: "Value", order: 1 )]
    public class ActivateActivityWithAttributes : ActionComponent
    {
        /// <summary>
        /// Executes the specified workflow.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="action">The action.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="errorMessages">The error messages.</param>
        /// <returns></returns>
        public override bool Execute( RockContext rockContext, WorkflowAction action, Object entity, out List<string> errorMessages )
        {
            errorMessages = new List<string>();

            Guid guid = GetAttributeValue( action, "Activity" ).AsGuid();
            if ( guid.IsEmpty() )
            {
                action.AddLogEntry( "Invalid Activity Property", true );
                return false;
            }

            var workflow = action.Activity.Workflow;

            var activityType = new WorkflowActivityTypeService( rockContext ).Queryable()
                .Where( a => a.Guid.Equals( guid ) ).FirstOrDefault();
            if ( activityType == null )
            {
                action.AddLogEntry( "Invalid Activity Property", true );
                return false;
            }

            Dictionary<string, string> keyValues = null;
            var attributeValues = GetAttributeValue( action, "AttributeValues" );
            if ( !string.IsNullOrWhiteSpace( attributeValues ) )
            {
                keyValues = attributeValues.AsDictionaryOrNull();
            }
            keyValues = keyValues ?? new Dictionary<string, string>();

            var activity = WorkflowActivity.Activate( activityType, workflow );
            activity.LoadAttributes( rockContext );

            foreach ( var keyPair in keyValues )
            {
                //
                // Does the key exist as an attribute in the destination activity?
                //
                if ( activity.Attributes.ContainsKey( keyPair.Key ) )
                {
                    var value = keyPair.Value.ResolveMergeFields( GetMergeFields( action ) );
                    activity.SetAttributeValue( keyPair.Key, value );
                }
                else
                {
                    errorMessages.Add( string.Format( "'{0}' is not an attribute key in the activated activity: '{1}'", keyPair.Value, activityType.Name ) );
                }
            }

            action.AddLogEntry( string.Format( "Activated new '{0}' activity", activityType.ToString() ) );

            return true;
        }

    }
}