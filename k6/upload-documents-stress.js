import http from "k6/http";
import { check, sleep } from "k6";

const payload = open("/data/payload-100k.csv");

export const options = {
  stages: [
    { duration: "10s", target: 5 },
    { duration: "30s", target: 10 },
    { duration: "10s", target: 0 },
  ],
  insecureSkipTLSVerify: true,
  thresholds: {
    http_req_failed: ["rate<0.01"],
    "http_req_duration{name:GenerateURL}": ["p(95)<200"],
    "http_req_duration{name:UploadToS3}": ["p(95)<1500"],
  },
};

const BASE_URL = "http://distributedfileprocessor.api:8080";

export default function () {
  const apiRes = http.post(
    `${BASE_URL}/api/documents/upload`,
    JSON.stringify({
      fileName: `stress-heavy-vu${__VU}-iter${__ITER}.csv`,
      contentType: "text/csv",
    }),
    {
      headers: { "Content-Type": "application/json" },
      tags: { name: "GenerateURL" },
    },
  );

  const isSuccess = check(apiRes, {
    "API returned Success (2xx)": (res) =>
      res.status >= 200 && res.status < 300,
  });

  if (isSuccess) {
    let uploadUrl = apiRes.json("url");

    uploadUrl = uploadUrl
      .replace("localhost:4566", "localstack:4566")
      .replace("127.0.0.1:4566", "localstack:4566");

    const s3Res = http.put(uploadUrl, payload, {
      headers: { "Content-Type": "text/csv" },
      tags: { name: "UploadToS3" },
    });

    check(s3Res, {
      "S3 heavy upload successful": (res) => res.status === 200,
    });
  }

  sleep(1);
}
