
export interface RunTestsParams {
  tests: TestInfo[];
}

export interface TestInfo {
  id: string;
  filePath: string;
  parentId?: string;
  pickleIndex?: number;
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


