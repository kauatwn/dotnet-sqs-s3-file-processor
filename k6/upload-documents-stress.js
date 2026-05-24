import http from "k6/http";
import { check, sleep } from "k6";

const payload = open("/data/payload-100k.csv");

export const options = {
  vus: 10,
  duration: "30s",
  insecureSkipTLSVerify: true,
};

const BASE_URL = "http://distributedfileprocessor.api:8080";

export default function () {
  const response = http.post(
    `${BASE_URL}/api/documents/upload`,
    JSON.stringify({
      fileName: `stress-heavy-${__ITER}.csv`,
      contentType: "text/csv",
    }),
    { headers: { "Content-Type": "application/json" } },
  );

  const isSuccess = check(response, {
    "API returned Success (2xx)": (res) =>
      res.status >= 200 && res.status < 300,
  });

  if (isSuccess) {
    let uploadUrl = response.json("url");

    // Corrige o mapeamento de rede interna do Docker substituindo o host local
    // pelo nome do container/serviço mapeado no docker-compose.
    uploadUrl = uploadUrl
      .replace("localhost:4566", "localstack:4566")
      .replace("127.0.0.1:4566", "localstack:4566");

    const s3Res = http.put(uploadUrl, payload, {
      headers: { "Content-Type": "text/csv" },
    });

    check(s3Res, {
      "S3 heavy upload successful": (res) => res.status === 200,
    });
  }

  sleep(1);
}
