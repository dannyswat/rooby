// Mirrors response/request records in src/Rooby.Api (Auth, Projects, Profiles, Schemas, Versioning).

export type ItemType =
  | 'SingleValue'
  | 'Lookup'
  | 'Matrix'
  | 'Basket'
  | 'ExpressionRule'
  | 'DecisionTable'
  | 'DecisionTree'
  | 'RuleList'

export const ITEM_TYPES: ItemType[] = [
  'SingleValue',
  'Lookup',
  'Matrix',
  'Basket',
  'ExpressionRule',
  'DecisionTable',
  'DecisionTree',
  'RuleList',
]

export type DataType = 'Boolean' | 'Number' | 'String' | 'List' | 'Date' | 'Object'

export const DATA_TYPES: DataType[] = ['Boolean', 'Number', 'String', 'List', 'Date', 'Object']

export type TestGate = 'None' | 'Warn' | 'Block'

export const TEST_GATES: TestGate[] = ['None', 'Warn', 'Block']

export type ChangeKind = 'Added' | 'Modified' | 'Deleted'

export const ACCESS_FLAGS = [
  'Read',
  'Edit',
  'Publish',
  'ManageProfile',
  'ManageSchema',
  'ManageProject',
  'SystemAdmin',
] as const

export type AccessFlag = (typeof ACCESS_FLAGS)[number]

export interface MeResponse {
  id: number
  loginName: string
  displayName: string
  systemAccess: string
}

export interface UserResponse {
  id: number
  loginName: string
  displayName: string
  isDisabled: boolean
}

export interface UserAccessResponse {
  id: number
  userId: number
  projectId: string | null
  profileId: string | null
  accessLevel: string
  isRevoked: boolean
}

export interface GrantAccessRequest {
  userId: number
  projectId: string | null
  profileId: string | null
  accessLevel: string
}

export interface ProjectResponse {
  id: string
  code: string
  name: string
  isDisabled: boolean
  remark: string
}

export interface CreateProjectRequest {
  code: string
  name: string
  remark?: string
}

export interface UpdateProjectRequest {
  name: string
  isDisabled: boolean
  remark?: string
}

export interface ProfileResponse {
  id: string
  code: string
  name: string
  timeZone: string
  publishUri: string | null
  hasPublishSecret: boolean
  testGate: TestGate
  isDisabled: boolean
  remark: string
  eTag: string
}

export interface CreateProfileRequest {
  code: string
  name: string
  timeZone?: string
  publishUri?: string
  publishSecret?: string
  remark?: string
}

export interface UpdateProfileRequest {
  name: string
  timeZone: string
  publishUri?: string
  publishSecret?: string
  testGate: TestGate
  isDisabled: boolean
  remark?: string
}

export interface SchemaResponse {
  id: string
  code: string
  name: string
  validFrom: string
  validTo: string | null
  definition: unknown
  eTag: string
}

export interface SchemaRequest {
  code: string
  name: string
  validFrom: string
  validTo: string | null
  definition: unknown
}

export interface ItemResponse {
  id: string
  key: string
  itemType: ItemType
  dataType: DataType
  schemaId: string | null
  description: string
  content: unknown
  isDeleted: boolean
  versionId: number
  eTag: string
}

export interface CreateItemRequest {
  key: string
  itemType: ItemType
  dataType: DataType
  schemaId?: string | null
  description: string
  content: unknown
}

export interface UpdateItemRequest {
  key: string
  description: string
  content: unknown
}

export interface LineResponse {
  id: string
  itemId: string
  sortOrder: number
  schemaId: string | null
  inheritSchema: boolean
  validFrom: string | null
  validTo: string | null
  remarks: string
  content: unknown
  isDeleted: boolean
  eTag: string
}

export interface LineRequest {
  schemaId?: string | null
  inheritSchema: boolean
  validFrom?: string | null
  validTo?: string | null
  remarks: string
  content: unknown
}

export interface TestCaseResponse {
  id: string
  itemKey: string
  inputData: unknown
  outputValue: unknown
  remarks: string
  isDeleted: boolean
  eTag: string
}

export interface TestCaseRequest {
  itemKey: string
  inputData: unknown
  outputValue: unknown
  remarks: string
}

export interface LineDiff {
  lineId: string
  change: ChangeKind
  before: unknown
  after: unknown
}

export interface ItemDiff {
  itemId: string
  key: string
  change: ChangeKind
  before: unknown
  after: unknown
  lines: LineDiff[]
}

export interface SchemaUpdate {
  schemaId: string
  code: string
}

export interface DraftDiff {
  items: ItemDiff[]
  schemaUpdates: SchemaUpdate[]
}

export interface VersionResponse {
  versionId: number
  fromVersionId: number
  description: string
  publishedAt: string | null
}

export interface VersionSnapshotResponse {
  items: ItemResponse[]
  lines: LineResponse[]
  testCases: TestCaseResponse[]
}

export interface RotateApiKeyResponse {
  apiKey: string
}

export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: { path: string; code: string; detail: string }[]
}
