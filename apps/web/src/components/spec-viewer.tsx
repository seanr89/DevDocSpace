"use client";

import dynamic from "next/dynamic";
import { useMemo } from "react";
import type SwaggerUIType from "swagger-ui-react";
import "swagger-ui-react/swagger-ui.css";
import { rewriteToProxy, type ApiEnvironment } from "@/lib/api";
import { useAuth } from "@/lib/auth";

const SwaggerUI = dynamic(() => import("swagger-ui-react"), { ssr: false });

type SwaggerProps = React.ComponentProps<typeof SwaggerUIType>;
type SwaggerRequest = Parameters<NonNullable<SwaggerProps["requestInterceptor"]>>[0];

export function SpecViewer({
  spec,
  service,
  version,
  env,
}: {
  spec: Record<string, unknown>;
  service: string;
  version: string;
  env: ApiEnvironment;
}) {
  const { firebaseUser } = useAuth();
  const serverKey = ((spec.servers as { url: string }[] | undefined) ?? []).map((s) => s.url).join("|");

  const requestInterceptor = useMemo(() => {
    const servers = serverKey ? serverKey.split("|") : [];
    return async (req: SwaggerRequest) => {
      req.url = rewriteToProxy(req.url as string, servers, service, version, env);
      const token = await firebaseUser?.getIdToken();
      if (token) (req.headers as Record<string, string>).Authorization = `Bearer ${token}`;
      return req;
    };
  }, [serverKey, service, version, env, firebaseUser]);

  return (
    <div className="swagger-wrapper">
      <SwaggerUI
        key={`${service}/${version}/${env}`}
        spec={spec}
        requestInterceptor={requestInterceptor}
        tryItOutEnabled
        persistAuthorization={false}
        docExpansion="list"
      />
    </div>
  );
}
