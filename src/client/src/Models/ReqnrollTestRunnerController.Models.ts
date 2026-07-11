export interface RunTestsParams {
  tests: TestInfo[];
  runId?: string;
}

export interface TestInfo {
  id: string;
  filePath: string;
  parentId?: string;
  pickleIndex?: number;
  isContainer?: boolean;
  label?: string;
}

export interface TestResult {
  id: string;
  passed: boolean;
  message?: string;
  line?: number;
}

export interface TestResultNotification {
  runId: string;
  result: TestResult;
}

export interface JsonRpcErrorLike {
  code?: number;
  message?: string;
  data?: unknown;
}


