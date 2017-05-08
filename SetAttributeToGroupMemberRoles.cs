// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
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
using Rock.Workflow;

namespace com.shepherdchurch.WorkflowActions
{
    /// <summary>
    /// Sets an attribute's value to the selected person 
    /// </summary>
    [ActionCategory( "Shepherd Church" )]
    [Description( "Set a Workflow attribute to the group type role they have for the group." )]
    [Export( typeof( ActionComponent ) )]
    [ExportMetadata( "ComponentName", "Set Attribute to Group Member Roles" )]

    [WorkflowAttribute( "Person", "Workflow attribute that contains the person to check for membership in the group.", true, "", "", 0, null,
        new string[] { "Rock.Field.Types.PersonFieldType" } )]

    [WorkflowAttribute( "Group", "Workflow Attribute that contains the group the person must be a member of.", true, "", "", 1, null,
        new string[] { "Rock.Field.Types.GroupFieldType" } )]

    [EnumsField( "Group Member Status", "Group member must be one of these status to match.", typeof( GroupMemberStatus ), true, "1,2", order: 2 )]

    [GroupRoleField( "", "Filter by Group Type Role", "The role the person must have in the group to be considered as valid membership.", false, order: 3, key: "GroupRole" )]

    [WorkflowAttribute( "Attribute", "The attribute to set with the group member roles.", true, "", "", 4, null, new string[] { "Rock.Field.Types.TextFieldType" } )]

    [BooleanField( "Store As Id", "Store the group type role Id(s) in the attribute instead of the role name(s).", false, "", 5 )]
    public class SetAttributeToGroupMemberRoles : ActionComponent
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

            // Determine which group to add the person to
            Group group = null;

            var guidGroupAttribute = GetAttributeValue( action, "Group" ).AsGuidOrNull();

            if ( guidGroupAttribute.HasValue )
            {
                var attributeGroup = AttributeCache.Read( guidGroupAttribute.Value, rockContext );
                if ( attributeGroup != null )
                {
                    var groupGuid = action.GetWorklowAttributeValue( guidGroupAttribute.Value ).AsGuidOrNull();

                    if ( groupGuid.HasValue )
                    {
                        group = new GroupService( rockContext ).Get( groupGuid.Value );
                    }
                }
            }

            if ( group == null )
            {
                errorMessages.Add( "No group was provided" );
            }

            // determine the person that will be added to the group
            Person person = null;

            // get the Attribute.Guid for this workflow's Person Attribute so that we can lookup the value
            var guidPersonAttribute = GetAttributeValue( action, "Person" ).AsGuidOrNull();

            if ( guidPersonAttribute.HasValue )
            {
                var attributePerson = AttributeCache.Read( guidPersonAttribute.Value, rockContext );
                if ( attributePerson != null )
                {
                    string attributePersonValue = action.GetWorklowAttributeValue( guidPersonAttribute.Value );
                    if ( !string.IsNullOrWhiteSpace( attributePersonValue ) )
                    {
                        if ( attributePerson.FieldType.Class == typeof( Rock.Field.Types.PersonFieldType ).FullName )
                        {
                            Guid personAliasGuid = attributePersonValue.AsGuid();
                            if ( !personAliasGuid.IsEmpty() )
                            {
                                person = new PersonAliasService( rockContext ).Queryable()
                                    .Where( a => a.Guid.Equals( personAliasGuid ) )
                                    .Select( a => a.Person )
                                    .FirstOrDefault();
                            }
                        }
                        else
                        {
                            errorMessages.Add( "The attribute used to provide the person was not of type 'Person'." );
                        }
                    }
                }
            }

            if ( person == null )
            {
                errorMessages.Add( string.Format( "Person could not be found for selected value ('{0}')!", guidPersonAttribute.ToString() ) );
            }

            //
            // Check if person is in the group.
            //
            if ( !errorMessages.Any() )
            {
                var groupMemberService = new GroupMemberService( rockContext );
                var groupMembers = groupMemberService.Queryable().Where( m => m.GroupId == group.Id && m.PersonId == person.Id ).ToList();
                var statuses = this.GetAttributeValue( action, "GroupMemberStatus" )
                    .SplitDelimitedValues()
                    .Select( s => ( GroupMemberStatus ) Enum.Parse( typeof( GroupMemberStatus ), s ) )
                    .ToList();

                groupMembers = groupMembers.Where( m => statuses.Contains( m.GroupMemberStatus ) ).ToList();

                if ( !string.IsNullOrWhiteSpace( GetAttributeValue( action, "GroupRole" ) ) )
                {
                    var groupRole = new GroupTypeRoleService( rockContext ).Get( GetAttributeValue( action, "GroupRole" ).AsGuid() );

                    groupMembers = groupMembers.Where( m => m.GroupRoleId == groupRole.Id ).ToList();
                }

                //
                // Set value of the selected attribute.
                //
                Guid selectAttributeGuid = GetAttributeValue( action, "Attribute" ).AsGuid();
                if ( !selectAttributeGuid.IsEmpty() )
                {
                    var selectedPersonAttribute = AttributeCache.Read( selectAttributeGuid, rockContext );
                    if ( selectedPersonAttribute != null )
                    {
                        if ( selectedPersonAttribute.FieldTypeId == FieldTypeCache.Read( Rock.SystemGuid.FieldType.BOOLEAN.AsGuid(), rockContext ).Id )
                        {
                            SetWorkflowAttributeValue( action, selectAttributeGuid, groupMembers.Any() ? "True" : "False" );
                        }
                        else if ( selectedPersonAttribute.FieldTypeId == FieldTypeCache.Read( Rock.SystemGuid.FieldType.INTEGER.AsGuid(), rockContext ).Id )
                        {
                            SetWorkflowAttributeValue( action, selectAttributeGuid, groupMembers.Any() ? "1" : "0" );
                        }
                        else if ( selectedPersonAttribute.FieldTypeId == FieldTypeCache.Read( Rock.SystemGuid.FieldType.TEXT.AsGuid(), rockContext ).Id )
                        {
                            if ( GetAttributeValue( action, "StoreAsId" ).AsBoolean( false ) == true )
                            {
                                SetWorkflowAttributeValue( action, selectAttributeGuid, string.Join( ",", groupMembers.Select( m => m.GroupRoleId.ToString() ) ) );
                            }
                            else
                            {
                                SetWorkflowAttributeValue( action, selectAttributeGuid, string.Join( ",", groupMembers.Select( m => m.GroupRole.Name ) ) );
                            }
                        }
                    }
                }
            }

            errorMessages.ForEach( m => action.AddLogEntry( m, true ) );

            return true;
        }
    }
}