
export interface StartBuildParams {
  referenceFileUri: string;
  fullRebuild:boolean;
}

export interface BuildResult {
  success: boolean;
  message: string;
  projectFile?: string;
}
