# IDEA

## Data Structure

- Project
- Profile
- User
- UserAccess
- Version
- Item
- ItemLine
- Schema
- TestCase

### Project

Rooby is a generic rule and configuration management tool. It allows managing multiple projects within the same company. Each project has its own schemas. It allows multiple profiles under a project.

#### Columns
- Id: Uuid
- Code: Varchar(20)
- Name: Nvarchar(100)
- IsDisabled: Boolean
- Created: UserLog
- LastModified: UserLog
- Remark: Nvarchar(4000)

### Profile

Each profile is managed by different users and has own set of configuration and rules. 

#### Columns
- Id: Uuid
- Code: Varchar(20)
- Name: Nvarchar(100)
- PublishUri: Nvarchar(500)
- IsDisabled: Boolean
- Created: UserLog
- LastModified: UserLog
- Remark: Nvarchar(4000)

### User

The user is an identity that may be integrated with enterprise identity service (e.g. AD, SAML, OIDC)

#### Columns
- Id: int
- LoginName: Varchar(20)
- LoginProvider: Flags
- DisplayName: Nvarchar(250)
- IsDisabled: Boolean
- Created: UserLog
- LastModified: UserLog

### UserAccess

It control which projects and profiles a user can access with permission control

#### Columns
- Id: int
- UserId: int
- ProjectId: Uuid?
- ProfileId: Uuid?
- AccessLevel: Flags
- Created: UserLog
- LastModified: UserLog

### Version

All configuration and rules are version controlled. All version snapshots can be easily extracted any time. It supports draft and stashes. VersionId > 0: Published version, VersionId = -1: Draft, VersionId < -1: Stashes

#### Columns
- Id: int
- ProjectId: Uuid
- ProfileId: Uuid
- VersionId: int
- FromVersionId: int
- Description: Nvarchar(100)
- Created: UserLog
- Published: UserLog

### Item

Item is a master record of a configuration or rule. 

#### Columns
- Id: Uuid
- ProfileId: Uuid
- Key: Varchar(30)
- VersionId: int
- ItemType: Enum (SingleValue, Lookup, RuleList, DecisionTree, DecisionTable, ExpressionRule, Basket)
- DataType: Enum (Boolean, Number, String, List)
- IsDeleted: Boolean
- SchemaId: Uuid
- Description: Nvarchar(100)
- Content: JSON
- Created: UserLog
- LastModified: UserLog
- Deleted: UserLog

### ItemLine

It is detail records of an item. It can be a lookup entry or a subset of rule in a rule list or a row in decision table.

#### Columns
- Id: Uuid
- ProfileId: Uuid
- ItemId: Uuid
- VersionId: int
- SortOrder: int
- IsDeleted: Boolean
- SchemaId: Uuid?
- InheritSchema: Boolean
- Remarks: Nvarchar(500)
- Content: JSON
- Created: UserLog
- LastModified: UserLog
- Deleted: UserLog

### Schema

It is the data definitions for a project.

#### Columns
- Id: Uuid
- ProjectId: Uuid
- Name: Nvarchar(100)
- Validity: Period
- Definition: JSON
- Created: UserLog
- LastModified: UserLog

### TestCase

It makes sure all the rules return expected results.

#### Columns
- Id: Uuid
- ProfileId: Uuid
- VersionId: int
- ItemKey: Varchar(30)
- InputData: JSON
- OutputValue: JSON
- Remarks: Nvarchar(4000)
- Created: UserLog
- LastModified: UserLog