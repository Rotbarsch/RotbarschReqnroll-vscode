
export interface StartBuildParams {
  featureFileUri: string;
  fullRebuild:boolean;
}

export interface BuildResult {
  success: boolean;
  message: string;
  projectFile?: string;
}
