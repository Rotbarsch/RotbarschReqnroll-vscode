
export interface StartBuildParams {
  featureFileUri: string;
}

export interface BuildResult {
  success: boolean;
  message: string;
  projectFile?: string;
}
