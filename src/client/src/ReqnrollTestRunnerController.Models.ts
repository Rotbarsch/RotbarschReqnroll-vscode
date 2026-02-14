
export interface RunTestsParams {
  tests: TestInfo[];
}

export interface TestInfo {
  id: string;
  filePath: string;
}

export interface TestResult {
  id: string;
  passed: boolean;
  message?: string;
  line?: number;
}

export interface JsonRpcErrorLike {
  code?: number;
  message?: string;
  data?: unknown;
}


